import geopandas as gpd
import numpy as np
import pandas as pd
import pytest
from shapely.geometry import LineString, box

from jobs.road_risk import pipeline
from jobs.road_risk.rainfall import IMERGProvider
from jobs.road_risk.surface_risk import EdgeRiskEnricher

PLACE = "Test Park, Tanzania"


class FakeIMERG(IMERGProvider):
    def __init__(self, latest="2026-11-14", counts=None):
        super().__init__()
        self._latest = latest
        self.granule_counts.update(counts or {"2026-11-14": 8})

    def latest_date(self, on_or_before, search_days=60):
        return None if self._latest is None else pd.Timestamp(self._latest)

    def granule_coverage(self, day):
        return self.granule_counts.get(day.strftime("%Y-%m-%d"), self.GRANULES_PER_DAY)


def fake_edges():
    return gpd.GeoDataFrame(
        {
            "road_type": ["track", ["primary", "track"]],
            "length_m": [120.0, 80.0],
            "travel_time_m": [0.4, 0.2],
            "rain_mm": [12.5, np.nan],
            "surface_risk": [0.45, 0.1],
        },
        geometry=[LineString([(36.0, -4.0), (36.1, -4.0)]), LineString([(36.1, -4.0), (36.2, -4.0)])],
        index=pd.MultiIndex.from_tuples([(1, 2, 0), (2, 3, 0)], names=["u", "v", "key"]),
        crs="EPSG:4326",
    )


@pytest.fixture
def fake_imerg():
    return FakeIMERG


@pytest.fixture
def place():
    return PLACE


@pytest.fixture
def place_dir(tmp_path):
    return tmp_path / "road-risk" / "test-park-tanzania"


@pytest.fixture
def computed(monkeypatch):
    calls = []

    def fake_compute(place, day, lookback_days, provider):
        calls.append((place, day, lookback_days, type(provider).__name__))
        return None, fake_edges()

    monkeypatch.setattr(pipeline, "compute_road_risk", fake_compute)
    return calls


@pytest.fixture
def enriched():
    pixels = gpd.GeoDataFrame(
        {"pixel_id": [0, 1], "rain_mm": [2.0, 40.0]},
        geometry=[box(36.0, -4.1, 36.1, -4.0), box(36.1, -4.1, 36.2, -4.0)],
        crs="EPSG:4326",
    )
    edges = gpd.GeoDataFrame(
        {"road_type": ["track", "primary", ["unclassified", "track"], "track"]},
        geometry=[
            LineString([(36.02, -4.05), (36.08, -4.05)]),
            LineString([(36.12, -4.05), (36.18, -4.05)]),
            LineString([(36.05, -4.05), (36.15, -4.05)]),
            LineString([(37.0, -4.05), (37.1, -4.05)]),
        ],
        index=pd.MultiIndex.from_tuples([(1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0)], names=["u", "v", "key"]),
        crs="EPSG:4326",
    )
    return EdgeRiskEnricher().enrich(edges, pixels)
