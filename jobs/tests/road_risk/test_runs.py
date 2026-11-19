import json
from datetime import date

from jobs.road_risk import runs
from jobs.shared import outputs


def test_run_road_risk_writes_geojson_and_manifest(tmp_path, computed, fake_imerg, place, place_dir):
    manifest = runs.run_road_risk("road-risk-refresh", place, date(2026, 11, 14), 3, fake_imerg(), tmp_path)
    geojson = json.loads((place_dir / "2026-11-14" / "edges.geojson").read_text())

    assert computed == [(place, date(2026, 11, 14), 3, "FakeIMERG")]
    assert [f["id"] for f in geojson["features"]] == ["1-2-0", "2-3-0"]
    assert geojson["features"][1]["properties"] == {
        "road_type": "primary",
        "length_m": 80.0,
        "travel_time_m": 0.2,
        "rain_mm": None,
        "surface_risk": 0.1,
    }
    assert manifest["date"] == "2026-11-14" and manifest["provider"] == "IMERG" and "rain_through" not in manifest
    assert manifest["granule_coverage"] == {"2026-11-12": 48, "2026-11-13": 48, "2026-11-14": 8}
    assert manifest["granules_per_day"] == 48 and manifest["complete"] is False
    assert outputs.read_latest(place_dir)["run"] == "2026-11-14"
    assert outputs.read_state(place_dir) is None


def test_complete_run_advances_the_state(tmp_path, computed, fake_imerg, place, place_dir):
    runs.run_road_risk("road-risk-backfill", place, date(2026, 11, 13), 5, fake_imerg(), tmp_path)
    state = outputs.read_state(place_dir)
    assert state["complete_through"] == "2026-11-13" and state["updated_by"] == "road-risk-backfill"
    assert runs.complete_through(place_dir) == date(2026, 11, 13)
    assert runs.is_complete(place_dir / "2026-11-13")


def test_latest_rain_day_never_after_today(fake_imerg):
    assert runs.latest_rain_day(fake_imerg(latest="2026-11-16"), date(2026, 11, 15)) == date(2026, 11, 15)
    assert runs.latest_rain_day(fake_imerg(latest="2026-11-13"), date(2026, 11, 15)) == date(2026, 11, 13)
