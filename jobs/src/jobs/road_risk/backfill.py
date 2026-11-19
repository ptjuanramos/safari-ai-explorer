from datetime import date, timedelta

from jobs.road_risk.config import RoadRiskConfig
from jobs.road_risk.rainfall import IMERGProvider
from jobs.road_risk.runs import day_range, is_complete, job_dir, latest_rain_day, run_road_risk
from jobs.shared import cli

BACKFILL_DAYS = 30


def backfill(
    place: str,
    start: date,
    end: date,
    lookback_days: int,
    data_dir: str,
    overwrite: bool = False,
    provider: IMERGProvider | None = None,
) -> dict[str, str]:
    provider = provider or IMERGProvider()
    results = {}
    days = day_range(start, end)
    for i, day in enumerate(days, start=1):
        key = day.isoformat()
        if not overwrite and is_complete(job_dir(data_dir, place) / key):
            results[key] = "exists"
        else:
            try:
                manifest = run_road_risk("road-risk-backfill", place, day, lookback_days, provider, data_dir)
                results[key] = f"{manifest['edge_count']} edges" + ("" if manifest["complete"] else ", incomplete")
            except ValueError as e:
                results[key] = f"skipped: {e}"
        print(f"[{i}/{len(days)}] {key}: {results[key]}")
    return results


def default_range(today: date, newest: date | None, start: date | None, end: date | None) -> tuple[date, date]:
    return start or today - timedelta(days=BACKFILL_DAYS), end or newest or today


def main(argv: list[str] | None = None) -> int:
    p = cli.add_road_risk_args(
        cli.parser(
            f"Road surface risk for past dates with IMERG (run manually), per place. "
            f"Defaults to the last {BACKFILL_DAYS} days."
        )
    )
    p.add_argument("--start", type=cli.parse_date)
    p.add_argument("--end", type=cli.parse_date)
    p.add_argument("--overwrite", action="store_true")
    args = p.parse_args(argv)

    places = RoadRiskConfig.places_to_run(args.settings, args.place)
    cli.init_earth_engine(args.ee_project)
    cli.use_osmnx_cache(args.cache_dir)
    today = cli.today_utc()
    # One provider for every place: it remembers each day's granule count, so that is asked once per day.
    provider = IMERGProvider()
    newest = None if args.end else latest_rain_day(provider, today)
    start, end = default_range(today, newest, args.start, args.end)
    if end < start:
        p.error(f"the range ends ({end}) before it starts ({start})")

    ok = True
    for place in places:
        print(f"== {place}")
        try:
            results = backfill(place, start, end, args.lookback_days, args.data_dir, args.overwrite, provider)
            ok &= not any(r.startswith("skipped") for r in results.values())
        except Exception as e:  # noqa: BLE001 - one place failing (Overpass, geocoding) mustn't stop the others
            print(f"{place}: failed ({type(e).__name__}: {e})")
            ok = False
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
