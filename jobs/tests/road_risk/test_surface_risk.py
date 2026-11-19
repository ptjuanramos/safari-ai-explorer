import numpy as np
import pytest

from jobs.road_risk.surface_risk import RainfallRiskModel, RoadRainVulnerability


@pytest.mark.parametrize(
    "mm, risk", [(np.nan, 0.0), (0, 0.0), (4.9, 0.0), (5, 0.3), (15, 0.6), (29.9, 0.6), (30, 1.0), (100, 1.0)]
)
def test_rainfall_risk_thresholds(mm, risk):
    assert RainfallRiskModel().risk_for(mm) == risk


def test_road_vulnerability_lookup():
    v = RoadRainVulnerability()
    assert v.factor_for("track") == 1.0
    assert v.factor_for(["secondary", "track"]) == 0.3
    assert v.factor_for("bridleway") == v.default


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
