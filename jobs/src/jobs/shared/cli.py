import argparse
import os
from datetime import date, datetime, timezone

DEFAULT_SETTINGS = "jobs/settings.toml"


def parser(description: str) -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=description)
    p.add_argument("--data-dir", default=os.environ.get("DATA_DIR", "data"))
    p.add_argument("--cache-dir", default=os.environ.get("CACHE_DIR", "cache"))
    p.add_argument("--settings", default=os.environ.get("SETTINGS_FILE", DEFAULT_SETTINGS))
    return p


def add_road_risk_args(p: argparse.ArgumentParser) -> argparse.ArgumentParser:
    p.add_argument(
        "--place",
        default=os.environ.get("PLACE") or None,
        help="only this OSM place (default: every place in settings.toml [road_risk])",
    )
    p.add_argument("--lookback-days", type=int, default=int(os.environ.get("LOOKBACK_DAYS", "5")))
    p.add_argument("--ee-project", default=os.environ.get("EE_PROJECT"))
    return p


def parse_date(text: str) -> date:
    return today_utc() if not text else date.fromisoformat(text)


def today_utc() -> date:
    return datetime.now(timezone.utc).date()


def init_earth_engine(project: str | None) -> None:
    import ee

    if not project:
        raise SystemExit("set --ee-project or EE_PROJECT to your Earth Engine project id")
    ee.Initialize(project=project)


def use_osmnx_cache(cache_dir: str) -> None:
    import osmnx as ox

    ox.settings.cache_folder = cache_dir
