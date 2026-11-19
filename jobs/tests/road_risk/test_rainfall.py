import numpy as np
import pandas as pd
import pytest

from jobs.road_risk.rainfall import AntecedentPrecipitationIndex, IMERGProvider, decayed_rain


def test_api_decay_weights():
    api = AntecedentPrecipitationIndex()
    assert api._recession_coefficient(20.0, 20.0) == pytest.approx(0.84)
    assert api._compute_single(0, 50.0, 20.0, 20.0) == 50.0
    assert api._compute_single(2, 50.0, 20.0, 20.0) == pytest.approx(50.0 * 0.84**2)


def test_decayed_rain_oldest_band_decays_most():
    api = AntecedentPrecipitationIndex()
    bands = ["d1_precipitation", "d2_precipitation", "d3_precipitation"]
    assert decayed_rain({"d1_precipitation": 10.0}, bands, api) == pytest.approx(10.0 * 0.84**2)
    assert decayed_rain({"d3_precipitation": 10.0}, bands, api) == pytest.approx(10.0)
    assert decayed_rain({"d2_precipitation": None}, bands, api) == 0.0


def test_frame_grid_covers_bounds_on_pixel_lattice():
    provider = IMERGProvider()
    bounds = (35.82, -4.57, 36.31, -3.93)
    grid = provider.frame_grid(bounds)
    minx, miny, maxx, maxy = grid.total_bounds
    assert minx <= bounds[0] and miny <= bounds[1] and maxx >= bounds[2] and maxy >= bounds[3]
    r = provider.resolution_deg
    assert np.allclose(np.round(grid.bounds["minx"] / r), grid.bounds["minx"] / r)
    assert np.allclose(grid.bounds["maxx"] - grid.bounds["minx"], r)
    assert np.allclose(grid.bounds["maxy"] - grid.bounds["miny"], r)
    assert grid["pixel_id"].is_unique


def test_imerg_granule_coverage_is_counted_once(monkeypatch):
    calls = []

    class FakeCollection:
        def size(self):
            return self

        def getInfo(self):
            calls.append(1)
            return 8

    provider = IMERGProvider()
    monkeypatch.setattr(provider, "_half_hourly", lambda day: FakeCollection())
    day = pd.Timestamp("2026-11-13")
    assert provider.granule_coverage(day) == 8
    assert provider.granule_coverage(day) == 8
    assert provider.granule_counts == {"2026-11-13": 8}
    assert len(calls) == 1


def test_imerg_day_without_granules_raises(monkeypatch):
    provider = IMERGProvider()
    monkeypatch.setattr(provider, "granule_coverage", lambda day: 0)
    monkeypatch.setattr(provider, "_half_hourly", lambda day: None)
    with pytest.raises(ValueError, match="no IMERG granules"):
        provider._daily_image(pd.Timestamp("2026-11-15"))
