from datetime import date
from functools import cache

import geopandas as gpd

from jobs.road_risk.network import ParkRoadNetwork
from jobs.road_risk.rainfall import RainGridProvider
from jobs.road_risk.surface_risk import EdgeRiskEnricher

EXPORT_COLUMNS = ["road_type", "length_m", "travel_time_m", "rain_mm", "surface_risk"]


@cache
def park_network(place_name: str) -> tuple[gpd.GeoDataFrame, gpd.GeoDataFrame]:
    """The place's nodes and edges, built once per process: a backfill reuses them for every day it computes."""
    return ParkRoadNetwork(place_name).load()


def compute_road_risk(
    place_name: str, day: date, lookback_days: int, rain_provider: RainGridProvider
) -> tuple[gpd.GeoDataFrame, gpd.GeoDataFrame]:
    nodes, edges = park_network(place_name)
    # Copies, so a day's rain columns never leak into the cached network.
    nodes, edges = nodes.copy(), edges.copy()

    pixel_grid = rain_provider.frame_grid(edges.total_bounds)
    rain_pixels, _ = rain_provider.get_decayed_precipitation(
        pixel_grid,
        date=day.strftime("%Y-%m-%d"),
        lookback_days=lookback_days,
    )

    return nodes, EdgeRiskEnricher().enrich(edges, rain_pixels)


def first_road_type(road_type) -> str:
    return str(road_type[0] if isinstance(road_type, list) else road_type)


def export_edges(edges: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    out = edges.reset_index()
    out["id"] = out["u"].astype(str) + "-" + out["v"].astype(str) + "-" + out["key"].astype(str)
    out["road_type"] = out["road_type"].apply(first_road_type)
    return out.set_index("id")[EXPORT_COLUMNS + ["geometry"]].to_crs("EPSG:4326")
