from datetime import date, timedelta
from pathlib import Path

import pandas as pd

from jobs.road_risk import pipeline
from jobs.road_risk.rainfall import IMERGProvider
from jobs.shared.outputs import (
    MANIFEST,
    advance_state,
    read_json,
    read_state,
    run_exists,
    slug,
    update_latest,
    write_run,
)

JOB = "road-risk"
NOTE = (
    "rain_mm is the decayed rain index (each day's rain x 0.84^days_ago), not a plain sum. "
    "surface_risk is rule-based with hand-set weights and has not been validated against real road conditions."
)


def job_dir(data_dir: str | Path, place: str) -> Path:
    return Path(data_dir) / JOB / slug(place)


def day_range(start: date, end: date) -> list[date]:
    return [start + timedelta(days=i) for i in range((end - start).days + 1)]


def rain_window(day: date, lookback_days: int) -> list[date]:
    return day_range(day - timedelta(days=lookback_days - 1), day)


def run_road_risk(
    job: str, place: str, day: date, lookback_days: int, provider: IMERGProvider, data_dir: str | Path
) -> dict:
    _, edges = pipeline.compute_road_risk(place, day, lookback_days, provider)
    exported = pipeline.export_edges(edges)
    coverage = {d.isoformat(): provider.granule_coverage(pd.Timestamp(d)) for d in rain_window(day, lookback_days)}

    manifest = {
        "job": job,
        "place": place,
        "date": day.isoformat(),
        "provider": "IMERG",
        "lookback_days": lookback_days,
        "edge_count": len(exported),
        "complete": all(n >= provider.GRANULES_PER_DAY for n in coverage.values()),
        "granules_per_day": provider.GRANULES_PER_DAY,
        "granule_coverage": coverage,
        "note": NOTE,
    }

    place_dir = job_dir(data_dir, place)
    manifest = write_run(
        place_dir / day.isoformat(), {"edges.geojson": exported.to_json(na="null", drop_id=False)}, manifest
    )
    update_latest(place_dir, day.isoformat(), only_if_newer=True)
    if manifest["complete"]:
        advance_state(place_dir, day.isoformat(), job)
    return manifest


def is_complete(run_dir: Path) -> bool:
    return run_exists(run_dir) and read_json(run_dir / MANIFEST).get("complete", False)


def complete_through(place_dir: Path) -> date | None:
    state = read_state(place_dir)
    return None if state is None else date.fromisoformat(state["complete_through"])


def latest_rain_day(provider: IMERGProvider, today: date, search_days: int = 7) -> date | None:
    latest = provider.latest_date(pd.Timestamp(today), search_days=search_days)
    return None if latest is None else min(latest.date(), today)
