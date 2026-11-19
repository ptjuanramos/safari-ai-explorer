import math
from dataclasses import dataclass, field

import ee
import geopandas as gpd
import numpy as np
import pandas as pd
from ee import ImageCollection
from shapely.geometry import box


@dataclass
class DailyObservation:
    days_ago: int
    rain_mm: float
    t_clt: float
    t_avg: float


class AntecedentPrecipitationIndex:
    """
    Computes a decay-weighted Antecedent Precipitation Index (API) from daily
    rainfall, using a temperature-adaptive recession coefficient (Crozier & Eyles
    1980, extended by De Moraes et al. 2024).
    """

    def __init__(self, k_opt: float = 0.84, delta: float = 0.012):
        self.k_opt = k_opt
        self.delta = delta

    def _recession_coefficient(self, t_clt: float, t_avg: float) -> float:
        return self.k_opt + self.delta * (t_clt - t_avg)

    def _compute_single(self, d: int, current_rain_mm: float, t_clt: float, t_avg: float) -> float:
        return current_rain_mm * math.pow(self._recession_coefficient(t_clt, t_avg), d)

    def compute(self, daily_observations: list[DailyObservation]) -> float:
        return sum(self._compute_single(obs.days_ago, obs.rain_mm, obs.t_clt, obs.t_avg) for obs in daily_observations)


def decayed_rain(props: dict, band_names: list[str], api: AntecedentPrecipitationIndex) -> float:
    n_days = len(band_names)
    observations = [
        DailyObservation(
            days_ago=(n_days - 1) - i,
            rain_mm=props.get(band) or 0.0,
            t_clt=20.0,
            t_avg=20.0,  # TODO: fixed for now — no temperature term until this is validated
        )
        for i, band in enumerate(band_names)
    ]
    return api.compute(observations)


@dataclass
class RainGridProvider:
    """
    Shared grid/pixel_id contract for the rainfall providers.

    Each row of the grid contains:
        - `pixel_id`: Unique identifier for the grid cell.
        - `geometry`: Polygon representing the grid cell (EPSG:4326, aligned to the dataset pixels).
    """

    resolution_deg: float = 0.05
    scale_m: int = 5566

    COORDINATES_MODE = "EPSG:4326"

    def frame_grid(self, bounds: tuple[float, float, float, float]) -> gpd.GeoDataFrame:
        minx, miny, maxx, maxy = bounds
        r = self.resolution_deg

        # Frame coordinates in the ranges of each degree -> [floor(resolution_deg), ceil(resolution_deg + x_coordinate))
        # For example: [35.12, 12.23) -> [35.10, 12.20)

        minx = np.floor(minx / r) * r
        miny = np.floor(miny / r) * r
        maxx = np.ceil(maxx / r) * r
        maxy = np.ceil(maxy / r) * r

        xs = np.arange(minx, maxx, r)
        ys = np.arange(miny, maxy, r)

        rows = [
            {"pixel_id": i, "geometry": box(x, y, x + r, y + r)}
            for i, (x, y) in enumerate((x, y) for x in xs for y in ys)
        ]

        return gpd.GeoDataFrame(rows, crs=self.COORDINATES_MODE)

    def daily_images(self, start: pd.Timestamp, end: pd.Timestamp) -> ImageCollection:
        raise NotImplementedError

    def latest_date(self, on_or_before: pd.Timestamp, search_days: int = 60) -> pd.Timestamp | None:
        start = on_or_before - pd.Timedelta(days=search_days)
        end = on_or_before + pd.Timedelta(days=1)
        latest_ms = (
            ee.ImageCollection(self.COLLECTION_ID)
            .filterDate(start.strftime("%Y-%m-%d"), end.strftime("%Y-%m-%d"))
            .aggregate_max("system:time_start")
            .getInfo()
        )
        return None if latest_ms is None else pd.Timestamp(latest_ms, unit="ms").normalize()

    def _reduce(self, image: ee.Image, grid: gpd.GeoDataFrame) -> list[dict]:
        features = [
            ee.Feature(
                ee.Geometry.Rectangle(list(row.geometry.bounds)),
                {"pixel_id": int(row.pixel_id)},
            )
            for row in grid.itertuples()
        ]
        reduced = image.reduceRegions(
            collection=ee.FeatureCollection(features),
            reducer=ee.Reducer.mean(),
            scale=self.scale_m,
        ).getInfo()
        return [f["properties"] for f in reduced["features"]]

    def get_accumulated_precipitation(
        self, grid: gpd.GeoDataFrame, start_date: str, end_date: str
    ) -> tuple[gpd.GeoDataFrame, ImageCollection]:
        """
        rain_mm is the flat sum of daily rainfall from start_date to end_date (both inclusive).
        """
        image_collection = self.daily_images(pd.Timestamp(start_date), pd.Timestamp(end_date))
        accumulated_image = image_collection.sum().rename("rain_mm")

        rain_by_pixel = {p["pixel_id"]: p.get("mean") for p in self._reduce(accumulated_image, grid)}

        result = grid.copy()
        result["rain_mm"] = result["pixel_id"].map(rain_by_pixel)
        return result, image_collection

    def get_decayed_precipitation(
        self, grid: gpd.GeoDataFrame, date: str, lookback_days: int
    ) -> tuple[gpd.GeoDataFrame, ImageCollection]:
        """
        Same grid/pixel_id contract as get_accumulated_precipitation, but rain_mm
        is now a decay-weighted API value instead of a flat window sum.
        """
        if lookback_days < 1:
            raise ValueError("lookback_days must be at least 1")

        end = pd.Timestamp(date)
        start = end - pd.Timedelta(days=lookback_days - 1)
        image_collection = self.daily_images(start, end).sort("system:time_start")

        n_days = image_collection.size().getInfo()
        if n_days != lookback_days:
            raise ValueError(
                f"expected {lookback_days} daily images, got {n_days} "
                f"- check {type(self).__name__} coverage for this window"
            )

        stacked = image_collection.toBands()
        band_names = stacked.bandNames().getInfo()

        api = AntecedentPrecipitationIndex()
        decayed_by_pixel = {p["pixel_id"]: decayed_rain(p, band_names, api) for p in self._reduce(stacked, grid)}

        result = grid.copy()
        result["rain_mm"] = result["pixel_id"].map(decayed_by_pixel)
        return result, image_collection


@dataclass
class IMERGProvider(RainGridProvider):
    """
    Provider for GPM IMERG precipitation (NASA/GPM_L3/IMERG_V07, ~0.1°).

    IMERG is natively half-hourly in mm/hr, so daily images are built by summing each day's half-hourly
    granules * 0.5 before reducing to grid cells. Best for recent/current dates.
    """

    resolution_deg: float = 0.1  # IMERG native ~0.1°, vs CHIRPS 0.05°
    scale_m: int = 11132  # ~0.1° at equator, vs CHIRPS 5566

    COLLECTION_ID = "NASA/GPM_L3/IMERG_V07"
    GRANULES_PER_DAY = 48

    granule_counts: dict = field(default_factory=dict, repr=False, compare=False)

    def _half_hourly(self, day: pd.Timestamp) -> ImageCollection:
        day_start = day.strftime("%Y-%m-%d")
        day_end = (day + pd.Timedelta(days=1)).strftime("%Y-%m-%d")
        return ee.ImageCollection(self.COLLECTION_ID).filterDate(day_start, day_end).select("precipitation")

    def granule_coverage(self, day: pd.Timestamp) -> int:
        key = day.strftime("%Y-%m-%d")
        if key not in self.granule_counts:
            self.granule_counts[key] = self._half_hourly(day).size().getInfo()
        return self.granule_counts[key]

    def _daily_image(self, day: pd.Timestamp) -> ee.Image:
        day_start = day.strftime("%Y-%m-%d")
        half_hourly = self._half_hourly(day)

        if self.granule_coverage(day) == 0:
            raise ValueError(f"no IMERG granules available for {day_start}")

        return (
            half_hourly.map(lambda img: img.multiply(0.5))
            .sum()
            .rename("precipitation")
            .set("system:time_start", ee.Date(day_start).millis())
            .set("date_str", day_start)
        )

    def daily_images(self, start: pd.Timestamp, end: pd.Timestamp) -> ImageCollection:
        return ee.ImageCollection([self._daily_image(d) for d in pd.date_range(start, end, freq="D")])
