from datetime import date

import geopandas as gpd
import pandas as pd
from shapely.geometry import LineString, box

from jobs.road_risk import pipeline
from jobs.road_risk.network import ParkRoadNetwork
from jobs.road_risk.pipeline import export_edges


def test_export_edges_keeps_contract_columns_in_wgs84(enriched):
    exported = export_edges(enriched.assign(length_m=100.0, travel_time_m=0.5).to_crs("EPSG:32736"))
    assert list(exported.columns) == ["road_type", "length_m", "travel_time_m", "rain_mm", "surface_risk", "geometry"]
    assert exported.crs == "EPSG:4326"
    assert exported.index.tolist() == ["1-2-0", "2-3-0", "3-4-0", "4-5-0"]
    assert exported["road_type"].tolist() == ["track", "primary", "unclassified", "track"]


def test_a_place_network_is_loaded_once_and_each_day_gets_its_own_copy(monkeypatch):
    loads = []
    edges = gpd.GeoDataFrame(
        {"road_type": ["track"]},
        geometry=[LineString([(36.02, -4.05), (36.08, -4.05)])],
        index=pd.MultiIndex.from_tuples([(1, 2, 0)], names=["u", "v", "key"]),
        crs="EPSG:4326",
    )

    def load(self):
        loads.append(self.place_name)
        return gpd.GeoDataFrame(), edges

    class FakeRain:
        pixels = gpd.GeoDataFrame(
            {"pixel_id": [0], "rain_mm": [40.0]}, geometry=[box(36.0, -4.1, 36.1, -4.0)], crs=4326
        )

        def frame_grid(self, bounds):
            return self.pixels

        def get_decayed_precipitation(self, grid, date, lookback_days):
            return grid, None

    monkeypatch.setattr(ParkRoadNetwork, "load", load)
    pipeline.park_network.cache_clear()
    try:
        _, first = pipeline.compute_road_risk("Park A", date(2026, 11, 13), 5, FakeRain())
        _, second = pipeline.compute_road_risk("Park A", date(2026, 11, 14), 5, FakeRain())
        pipeline.compute_road_risk("Park B", date(2026, 11, 14), 5, FakeRain())
    finally:
        pipeline.park_network.cache_clear()

    assert loads == ["Park A", "Park B"]
    assert first is not second
    assert "surface_risk" not in edges.columns
