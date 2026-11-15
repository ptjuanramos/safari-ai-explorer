import time
from pathlib import Path

import pandas as pd
import requests

from .config import Config

GBIF_URL = "https://api.gbif.org/v1/occurrence/search"
MAMMALS_CLASS_KEY = 359
PAGE_SIZE = 300
COLUMNS = ["gbifID", "decimalLongitude", "decimalLatitude", "year", "speciesKey", "species"]


def get_page(params: dict, retries: int = 5, wait_s: float = 5) -> dict:
    for attempt in range(retries):
        try:
            r = requests.get(GBIF_URL, params=params, timeout=120)
            r.raise_for_status()
            return r.json()
        except requests.RequestException as e:
            print(f"retry {attempt + 1}/{retries} at offset {params['offset']}: {e}")
            time.sleep(wait_s * (attempt + 1))
    raise RuntimeError(f"GBIF failed {retries} times at offset {params['offset']}")


def fetch_all(params: dict) -> pd.DataFrame:
    rows, offset = [], 0
    while True:
        page = get_page({**params, "offset": offset, "limit": PAGE_SIZE})
        rows.extend(page["results"])
        if page["endOfRecords"]:
            break
        offset += PAGE_SIZE
    return pd.DataFrame(rows)


def cache_path(config: Config) -> Path | None:
    if config.cache_dir is None:
        return None
    west, south, east, north = config.bounds
    return Path(config.cache_dir) / f"mammals_{config.start_year}-{config.end_year}_{west}_{south}_{east}_{north}.parquet"


def fetch_mammal_records(config: Config = Config()) -> pd.DataFrame:
    path = cache_path(config)
    if path is not None and path.exists():
        print("loaded from", path)
        return pd.read_parquet(path)

    west, south, east, north = config.bounds
    params = {
        "classKey": MAMMALS_CLASS_KEY,
        "decimalLongitude": f"{west},{east}",
        "decimalLatitude": f"{south},{north}",
        "hasCoordinate": "true",
        "hasGeospatialIssue": "false",
    }
    yearly = []
    for year in range(config.start_year, config.end_year + 1):
        year_df = fetch_all({**params, "year": year}).reindex(columns=COLUMNS)
        print(year, len(year_df))
        yearly.append(year_df)
    records = pd.concat(yearly, ignore_index=True).astype({"gbifID": str, "species": str})

    if path is not None:
        path.parent.mkdir(parents=True, exist_ok=True)
        records.to_parquet(path, index=False)
    return records
