from datetime import date

from jobs.road_risk import backfill, pipeline
from jobs.shared import outputs


def test_backfill_defaults_to_the_last_month_up_to_the_newest_rain_day():
    today = date(2026, 11, 15)
    assert backfill.default_range(today, date(2026, 11, 14), None, None) == (date(2026, 10, 16), date(2026, 11, 14))
    assert backfill.default_range(today, None, date(2024, 4, 1), date(2024, 4, 30)) == (
        date(2024, 4, 1),
        date(2024, 4, 30),
    )


def test_backfill_writes_one_run_per_day_and_saves_the_last_complete_day(
    tmp_path, computed, fake_imerg, place, place_dir
):
    outputs.update_latest(place_dir, "2026-11-15")

    results = backfill.backfill(
        place, date(2026, 2, 27), date(2026, 3, 1), 5, tmp_path, provider=fake_imerg(counts={"2026-03-01": 47})
    )

    assert list(results) == ["2026-02-27", "2026-02-28", "2026-03-01"]
    assert results["2026-03-01"].endswith("incomplete")
    assert outputs.read_json(place_dir / "2026-02-28" / "manifest.json")["job"] == "road-risk-backfill"
    assert outputs.read_state(place_dir)["complete_through"] == "2026-02-28"
    assert outputs.read_latest(place_dir)["run"] == "2026-11-15"


def test_backfill_skips_complete_runs_unless_overwrite(tmp_path, computed, fake_imerg, place):
    partial = fake_imerg(counts={"2026-03-02": 8})
    backfill.backfill(place, date(2026, 3, 1), date(2026, 3, 2), 5, tmp_path, provider=partial)
    again = backfill.backfill(place, date(2026, 3, 1), date(2026, 3, 2), 5, tmp_path, provider=partial)
    assert again["2026-03-01"] == "exists" and again["2026-03-02"].endswith("incomplete")
    assert len(computed) == 3

    backfill.backfill(place, date(2026, 3, 1), date(2026, 3, 1), 5, tmp_path, overwrite=True, provider=partial)
    assert len(computed) == 4


def test_backfill_reports_days_without_rain_data(tmp_path, monkeypatch, fake_imerg, place, place_dir):
    def failing_compute(place, day, lookback_days, provider):
        raise ValueError("no IMERG granules available for 2026-03-01")

    monkeypatch.setattr(pipeline, "compute_road_risk", failing_compute)
    results = backfill.backfill(place, date(2026, 3, 1), date(2026, 3, 1), 5, tmp_path, provider=fake_imerg())
    assert results["2026-03-01"].startswith("skipped")
    assert not outputs.run_exists(place_dir / "2026-03-01")
    assert outputs.read_state(place_dir) is None
