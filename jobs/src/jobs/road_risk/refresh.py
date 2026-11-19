from datetime import date, timedelta

from jobs.road_risk.config import RoadRiskConfig
from jobs.road_risk.rainfall import IMERGProvider
from jobs.road_risk.runs import complete_through, day_range, job_dir, latest_rain_day, run_road_risk
from jobs.shared import cli


def days_to_refresh(last_complete: date | None, newest: date) -> list[date]:
    start = newest if last_complete is None else last_complete + timedelta(days=1)
    return day_range(start, newest)


def refresh_place(place: str, newest: date, lookback_days: int, provider: IMERGProvider, data_dir: str) -> None:
    """Computes every day of one place from its `complete_through + 1` to `newest`."""
    last = complete_through(job_dir(data_dir, place))
    if last is None:
        print(f"{place}: no state.json yet, only the newest day is computed; run road-risk-backfill for history")
    days = days_to_refresh(last, newest)
    if not days:
        print(f"{place}: complete through {last}, no newer IMERG day yet")
        return

    for day in days:
        manifest = run_road_risk("road-risk-refresh", place, day, lookback_days, provider, data_dir)
        status = "complete" if manifest["complete"] else "incomplete, redone on the next run"
        print(
            f"{place} {day}: {manifest['edge_count']} edges, "
            f"{manifest['granule_coverage'][day.isoformat()]}/48 granules, {status}"
        )


def main(argv: list[str] | None = None) -> int:
    args = cli.add_road_risk_args(
        cli.parser("Road surface risk for every IMERG day after the last complete one (state.json), per place.")
    ).parse_args(argv)
    places = RoadRiskConfig.places_to_run(args.settings, args.place)
    cli.init_earth_engine(args.ee_project)
    cli.use_osmnx_cache(args.cache_dir)

    today = cli.today_utc()
    # One provider for every place: it remembers each day's granule count, so that is asked once per day.
    provider = IMERGProvider()
    newest = latest_rain_day(provider, today)
    if newest is None:
        print(f"no IMERG data in the 7 days before {today}; nothing written")
        return 1

    failed = []
    for place in places:
        # One place failing (Overpass at capacity, a place name that doesn't geocode) mustn't stop the others.
        try:
            refresh_place(place, newest, args.lookback_days, provider, args.data_dir)
        except Exception as e:  # noqa: BLE001 - reported, and the exit code says something failed
            print(f"{place}: failed ({type(e).__name__}: {e}); the next run retries it")
            failed.append(place)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
