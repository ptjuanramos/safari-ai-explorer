import json
import time
from dataclasses import dataclass

import pandas as pd
import requests

from jobs.sightings.models import RECORD_COLUMNS


@dataclass
class SightingsService:
    """
    Talks to the occurrence API. Today that is GBIF. Another source would be a new service with the same
    `fetch_year`, returning the same `RECORD_COLUMNS`.

    Attributes:
        base_url: Occurrence search endpoint.
        page_size: Records per request (GBIF allows at most 300).
        retries: Attempts per page before giving up.
        wait_s: Wait before a retry, multiplied by the attempt number.
        timeout_s: Longest time one request may take, from start to end. `requests` alone only limits each wait
            between bytes, so a server that keeps sending a trickle could hold a request open for hours.
        chunk_bytes: Size of the pieces the answer is read in; the deadline is checked after each one.
        max_records: Most records fetched from one box. GBIF serves pages past offset 10,000 at about 4 KB/s, so a
            box holding more is split in halves until each part is under this.
        min_box_deg: Smallest box side before splitting gives up (a point can't be split further).
    """

    base_url: str = "https://api.gbif.org/v1/occurrence/search"
    page_size: int = 300
    retries: int = 5
    wait_s: float = 5
    timeout_s: float = 120
    chunk_bytes: int = 8192
    max_records: int = 10_000
    min_box_deg: float = 0.001

    def fetch_year(self, taxon_key: int, bounds: tuple[float, float, float, float], year: int) -> pd.DataFrame:
        """All records of the taxon with coordinates inside the box, for one year."""
        boxes = self.boxes_under_limit(taxon_key, bounds, year)
        frames = [self.fetch_all(self.search_params(taxon_key, box, year)) for box in boxes]
        records = pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()
        if "gbifID" in records:
            # GBIF's ranges include both ends, so a record on a split line comes back from both halves.
            records = records.drop_duplicates("gbifID", ignore_index=True)
        return records.reindex(columns=list(RECORD_COLUMNS)).astype(
            {
                "gbifID": str,
                "species": str,
                "coordinateUncertaintyInMeters": float,
            }
        )

    def boxes_under_limit(
        self, taxon_key: int, bounds: tuple[float, float, float, float], year: int
    ) -> list[tuple[float, float, float, float]]:
        """The box, or its halves split again and again, so no part holds more than `max_records`; empty parts
        are left out."""
        count = self.count_records(self.search_params(taxon_key, bounds, year))
        if count == 0:
            return []
        if count <= self.max_records:
            return [bounds]
        west, south, east, north = bounds
        if max(east - west, north - south) < self.min_box_deg:
            raise RuntimeError(f"{count} records in {bounds}, too small to split under {self.max_records}")
        return [box for half in self.halves(bounds) for box in self.boxes_under_limit(taxon_key, half, year)]

    def count_records(self, params: dict) -> int:
        """How many records match, from a page of zero records (fast at any size)."""
        return self.get_page({**params, "offset": 0, "limit": 0})["count"]

    @staticmethod
    def halves(bounds: tuple[float, float, float, float]) -> list[tuple[float, float, float, float]]:
        """The box cut in two across its longer side, so the parts stay close to square."""
        west, south, east, north = bounds
        if east - west >= north - south:
            middle = (west + east) / 2
            return [(west, south, middle, north), (middle, south, east, north)]
        middle = (south + north) / 2
        return [(west, south, east, middle), (west, middle, east, north)]

    def fetch_all(self, params: dict) -> pd.DataFrame:
        """Follows the pages until GBIF reports the end of the records."""
        rows, offset = [], 0
        while True:
            page = self.get_page({**params, "offset": offset, "limit": self.page_size})
            rows.extend(page["results"])
            if page["endOfRecords"]:
                return pd.DataFrame(rows)
            offset += self.page_size

    def get_page(self, params: dict) -> dict:
        """One request, retried with growing waits (deep pages often time out or return 503)."""
        for attempt in range(1, self.retries + 1):
            try:
                return self._get_json(params)
            except requests.RequestException as error:
                print(f"retry {attempt}/{self.retries} at offset {params['offset']}: {error}")
                time.sleep(self.wait_s * attempt)

        raise RuntimeError(f"GBIF failed {self.retries} times at offset {params['offset']}")

    def _get_json(self, params: dict) -> dict:
        """One page's JSON, read in chunks; raises `requests.Timeout` once the whole request passes `timeout_s`."""
        deadline = time.monotonic() + self.timeout_s
        with requests.get(self.base_url, params=params, timeout=self.timeout_s, stream=True) as response:
            response.raise_for_status()
            body = bytearray()
            for chunk in response.iter_content(chunk_size=self.chunk_bytes):
                body.extend(chunk)
                if time.monotonic() > deadline:
                    raise requests.Timeout(f"no full answer within {self.timeout_s:.0f} s")
        return json.loads(body)

    @staticmethod
    def search_params(taxon_key: int, bounds: tuple[float, float, float, float], year: int) -> dict:
        west, south, east, north = bounds
        return {
            "classKey": taxon_key,
            "decimalLongitude": f"{west},{east}",
            "decimalLatitude": f"{south},{north}",
            "hasCoordinate": "true",
            "hasGeospatialIssue": "false",
            "year": year,
        }
