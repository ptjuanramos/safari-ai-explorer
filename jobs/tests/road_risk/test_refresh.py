from datetime import date

import pytest

from jobs.road_risk import pipeline, refresh
from jobs.shared import cli, outputs


@pytest.fixture
def run_refresh(tmp_path, computed, fake_imerg, place, monkeypatch):
    monkeypatch.setattr(cli, "init_earth_engine", lambda project: None)
    monkeypatch.setattr(cli, "use_osmnx_cache", lambda cache_dir: None)
    monkeypatch.setattr(cli, "today_utc", lambda: date(2026, 11, 15))

    def run(latest, counts=None):
        class Configured(fake_imerg):
            def __init__(self):
                super().__init__(latest, counts)

        monkeypatch.setattr(refresh, "IMERGProvider", Configured)
        return refresh.main(["--data-dir", str(tmp_path), "--place", place, "--ee-project", "p"])

    return run


def test_days_to_refresh_start_after_the_saved_day():
    newest = date(2026, 11, 14)
    assert refresh.days_to_refresh(None, newest) == [newest]
    assert refresh.days_to_refresh(date(2026, 11, 11), newest) == [date(2026, 11, 12), date(2026, 11, 13), newest]
    assert refresh.days_to_refresh(newest, newest) == []


def test_refresh_continues_after_the_backfill(computed, place_dir, run_refresh):
    outputs.advance_state(place_dir, "2026-03-01", "road-risk-backfill")
    assert run_refresh("2026-03-04", {"2026-03-04": 8}) == 0
    assert [c[1] for c in computed] == [date(2026, 3, 2), date(2026, 3, 3), date(2026, 3, 4)]
    assert outputs.read_state(place_dir)["complete_through"] == "2026-03-03"


def test_refresh_redoes_a_partial_day_until_it_is_complete(computed, place_dir, run_refresh):
    assert run_refresh("2026-11-14", {"2026-11-14": 8}) == 0
    assert outputs.read_state(place_dir) is None
    assert run_refresh("2026-11-14", {"2026-11-14": 48}) == 0
    assert outputs.read_state(place_dir)["complete_through"] == "2026-11-14"
    assert run_refresh("2026-11-15", {"2026-11-15": 6}) == 0

    assert [c[1] for c in computed] == [date(2026, 11, 14), date(2026, 11, 14), date(2026, 11, 15)]
    assert outputs.read_json(place_dir / "2026-11-14" / "manifest.json")["complete"] is True
    assert outputs.read_latest(place_dir)["run"] == "2026-11-15"
    assert outputs.read_state(place_dir)["complete_through"] == "2026-11-14"


def test_refresh_does_nothing_without_a_newer_day(computed, place_dir, run_refresh):
    outputs.advance_state(place_dir, "2026-11-14", "road-risk-refresh")
    assert run_refresh("2026-11-14") == 0
    assert computed == []


def test_refresh_writes_nothing_without_recent_rain(computed, place_dir, run_refresh):
    outputs.update_latest(place_dir, "2026-11-14")
    assert run_refresh(None) == 1
    assert computed == []
    assert outputs.read_latest(place_dir)["run"] == "2026-11-14"


def test_refresh_runs_every_configured_place_and_one_failing_place_does_not_stop_the_rest(
    tmp_path, computed, fake_imerg, monkeypatch
):
    settings = tmp_path / "settings.toml"
    settings.write_text('[road_risk]\nplaces = ["Bad Park, Tanzania", "Good Park, Tanzania"]\n')
    monkeypatch.delenv("PLACE", raising=False)
    monkeypatch.setattr(cli, "init_earth_engine", lambda project: None)
    monkeypatch.setattr(cli, "use_osmnx_cache", lambda cache_dir: None)
    monkeypatch.setattr(cli, "today_utc", lambda: date(2026, 11, 15))
    monkeypatch.setattr(refresh, "IMERGProvider", lambda: fake_imerg("2026-11-14", {"2026-11-14": 48}))
    compute = pipeline.compute_road_risk

    def flaky(place, day, lookback_days, provider):
        if place.startswith("Bad"):
            raise RuntimeError("Overpass at capacity")
        return compute(place, day, lookback_days, provider)

    monkeypatch.setattr(pipeline, "compute_road_risk", flaky)

    assert refresh.main(["--data-dir", str(tmp_path), "--settings", str(settings), "--ee-project", "p"]) == 1
    assert outputs.read_latest(tmp_path / "road-risk" / "good-park-tanzania")["run"] == "2026-11-14"
    assert not (tmp_path / "road-risk" / "bad-park-tanzania").exists()
    assert [c[0] for c in computed] == ["Good Park, Tanzania"]
