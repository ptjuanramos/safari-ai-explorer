from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

import pandas as pd

from jobs.sightings.config import SightingsConfig
from jobs.sightings.models import RECORD_COLUMNS
from jobs.sightings.service import SightingsService


@dataclass
class Sightings:
    """
    The records the heat map is built from, fetched one year at a time and cached as one parquet file per year.

    Attributes:
        config: Box, years, taxon and cache folder.
        service: Where the records come from.
        refetch_years: The current year and the ones before it, this many in total, are always fetched again,
            because records are often uploaded months after the sighting. Older years are read from the cache.
    """

    config: SightingsConfig
    service: SightingsService = field(default_factory=SightingsService)
    refetch_years: int = 2

    def fetch(self, current_year: int | None = None) -> pd.DataFrame:
        current_year = current_year or date.today().year
        end_year = self.config.end_year or current_year

        yearly = []
        for year in range(self.config.start_year, end_year + 1):
            records = self._fetch_year(year, cacheable=year <= current_year - self.refetch_years)
            print(year, len(records))
            yearly.append(records)
        return pd.concat(yearly, ignore_index=True)

    def get_cache_path(self, year: int) -> Path | None:
        if self.config.cache_dir is None:
            return None
        west, south, east, north = self.config.bounds
        name = f"sightings_{self.config.taxon_key}_{year}_{west}_{south}_{east}_{north}.parquet"
        return Path(self.config.cache_dir) / name

    def _fetch_year(self, year: int, cacheable: bool) -> pd.DataFrame:
        path = self.get_cache_path(year) if cacheable else None
        cached = self._read_cache(path)
        if cached is not None:
            return cached

        records = self.service.fetch_year(self.config.taxon_key, self.config.bounds, year)
        self._write_cache(path, records)
        return records

    @staticmethod
    def _read_cache(path: Path | None) -> pd.DataFrame | None:
        """The cached year, or None. A file with other columns was written by an older version: it is deleted."""
        if path is None or not path.exists():
            return None

        cached = pd.read_parquet(path)
        if list(cached.columns) == list(RECORD_COLUMNS):
            return cached

        path.unlink()
        return None

    @staticmethod
    def _write_cache(path: Path | None, records: pd.DataFrame) -> None:
        if path is None:
            return
        path.parent.mkdir(parents=True, exist_ok=True)
        records.to_parquet(path, index=False)
