import geopandas as gpd
import numpy as np
import pandas as pd
import pytest
from shapely.geometry import LineString, box

from safari_explorer.rainfall import AntecedentPrecipitationIndex, CHIRPSProvider, IMERGProvider, decayed_rain
from safari_explorer.risk_map import get_risk_color
from safari_explorer.road_network import ParkRoadNetwork
from safari_explorer.surface_risk import EdgeRiskEnricher, RainfallRiskModel, RoadRainVulnerability


def test_api_decay_weights():
    api = AntecedentPrecipitationIndex()
    assert api._recession_coefficient(20.0, 20.0) == pytest.approx(0.84)
    assert api._compute_single(0, 50.0, 20.0, 20.0) == 50.0
    assert api._compute_single(2, 50.0, 20.0, 20.0) == pytest.approx(50.0 * 0.84 ** 2)


def test_decayed_rain_oldest_band_decays_most():
    api = AntecedentPrecipitationIndex()
    bands = ["d1_precipitation", "d2_precipitation", "d3_precipitation"]
    assert decayed_rain({"d1_precipitation": 10.0}, bands, api) == pytest.approx(10.0 * 0.84 ** 2)
    assert decayed_rain({"d3_precipitation": 10.0}, bands, api) == pytest.approx(10.0)
    assert decayed_rain({"d2_precipitation": None}, bands, api) == 0.0


@pytest.mark.parametrize("mm, risk", [(np.nan, 0.0), (0, 0.0), (4.9, 0.0), (5, 0.3), (15, 0.6), (29.9, 0.6), (30, 1.0), (100, 1.0)])
def test_rainfall_risk_thresholds(mm, risk):
    assert RainfallRiskModel().risk_for(mm) == risk


def test_road_vulnerability_lookup():
    v = RoadRainVulnerability()
    assert v.factor_for("track") == 1.0
    assert v.factor_for(["secondary", "track"]) == 0.3
    assert v.factor_for("bridleway") == v.default


def test_vehicle_road_filter():
    network = ParkRoadNetwork("anywhere")
    assert network._is_vehicle_road("track")
    assert not network._is_vehicle_road("footway")
    assert not network._is_vehicle_road(["track", "path"])


@pytest.mark.parametrize("provider", [CHIRPSProvider(), IMERGProvider()])
def test_frame_grid_covers_bounds_on_pixel_lattice(provider):
    bounds = (35.82, -4.57, 36.31, -3.93)
    grid = provider.frame_grid(bounds)
    minx, miny, maxx, maxy = grid.total_bounds
    assert minx <= bounds[0] and miny <= bounds[1] and maxx >= bounds[2] and maxy >= bounds[3]
    r = provider.resolution_deg
    assert np.allclose(np.round(grid.bounds["minx"] / r), grid.bounds["minx"] / r)
    assert np.allclose(grid.bounds["maxx"] - grid.bounds["minx"], r)
    assert np.allclose(grid.bounds["maxy"] - grid.bounds["miny"], r)
    assert grid["pixel_id"].is_unique


@pytest.mark.parametrize("value, color", [(0.0, "#2ecc71"), (0.3, "#f39c12"), (0.6, "#e74c3c"), (0.9, "#7b241c"), (1.0, "#7b241c")])
def test_risk_colors(value, color):
    assert get_risk_color(value) == color


@pytest.fixture
def enriched():
    pixels = gpd.GeoDataFrame({"pixel_id": [0, 1], "rain_mm": [2.0, 40.0]},
                              geometry=[box(36.0, -4.1, 36.1, -4.0), box(36.1, -4.1, 36.2, -4.0)], crs="EPSG:4326")
    edges = gpd.GeoDataFrame(
        {"road_type": ["track", "primary", ["unclassified", "track"], "track"]},
        geometry=[LineString([(36.02, -4.05), (36.08, -4.05)]),
                  LineString([(36.12, -4.05), (36.18, -4.05)]),
                  LineString([(36.05, -4.05), (36.15, -4.05)]),
                  LineString([(37.0, -4.05), (37.1, -4.05)])],
        index=pd.MultiIndex.from_tuples([(1, 2, 0), (2, 3, 0), (3, 4, 0), (4, 5, 0)], names=["u", "v", "key"]),
        crs="EPSG:4326",
    )
    return EdgeRiskEnricher().enrich(edges, pixels)


def test_edge_takes_worst_pixel_it_touches(enriched):
    assert enriched["rain_mm"].tolist()[:3] == [2.0, 40.0, 40.0]
    assert np.isnan(enriched["rain_mm"].iloc[3])


def test_surface_risk_formula(enriched):
    expected = [
        min(1, 0.15 + 1.0 * 0.0),
        min(1, 0.0 + 0.1 * 1.0),
        min(1, 0.1 + 0.7 * 1.0),
        min(1, 0.15 + 1.0 * 0.0),
    ]
    assert enriched["surface_risk"].tolist() == pytest.approx(expected)
    assert enriched.crs == "EPSG:4326"
