from typing import ClassVar

import pandas as pd
from pyproj import Transformer

from jobs.shared.crs import UTM, WGS84


class Geo:
    """Coordinate helpers. Distances and grids are in UTM meters (ADR-0001); lon/lat is for input and display."""

    TO_UTM: ClassVar[Transformer] = Transformer.from_crs(WGS84, UTM, always_xy=True)
    TO_WGS84: ClassVar[Transformer] = Transformer.from_crs(UTM, WGS84, always_xy=True)

    @classmethod
    def with_utm_xy(
        cls,
        records: pd.DataFrame,
        lon: str = "decimalLongitude",
        lat: str = "decimalLatitude",
    ) -> pd.DataFrame:
        """Drops records without coordinates and adds `x`, `y` in UTM meters."""
        located = records.dropna(subset=[lon, lat]).copy()
        located["x"], located["y"] = cls.TO_UTM.transform(located[lon].to_numpy(), located[lat].to_numpy())
        return located

    @classmethod
    def wgs84_to_utm_bounds(cls, bounds: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
        """(west, south, east, north) in degrees -> (minx, miny, maxx, maxy) in UTM meters."""
        return cls.TO_UTM.transform_bounds(*bounds)

    @classmethod
    def utm_to_wgs84_bounds(
        cls,
        minx: float,
        miny: float,
        maxx: float,
        maxy: float,
    ) -> tuple[float, float, float, float]:
        """(minx, miny, maxx, maxy) in UTM meters -> (west, south, east, north) in degrees."""
        return cls.TO_WGS84.transform_bounds(minx, miny, maxx, maxy)
