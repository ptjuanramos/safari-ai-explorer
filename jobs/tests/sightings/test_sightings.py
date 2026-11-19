from dataclasses import replace

import pandas as pd
import pytest

from jobs.sightings.models import RECORD_COLUMNS
from jobs.sightings.sightings import Sightings


class FakeService:
    def __init__(self):
        self.years = []

    def fetch_year(self, taxon_key, bounds, year):
        self.years.append(year)
        row = {
            "gbifID": "1",
            "decimalLongitude": 34.8,
            "decimalLatitude": -2.4,
            "coordinateUncertaintyInMeters": 10.0,
            "year": year,
            "speciesKey": 2441034,
            "species": "Syncerus caffer",
        }
        return pd.DataFrame([row], columns=list(RECORD_COLUMNS))


@pytest.fixture
def sightings(config, tmp_path):
    def make(start_year, end_year):
        return Sightings(
            replace(config, cache_dir=str(tmp_path), start_year=start_year, end_year=end_year), service=FakeService()
        )

    return make


def test_past_years_are_cached(sightings):
    s = sightings(2020, 2021)
    first = s.fetch(current_year=2030)
    second = s.fetch(current_year=2030)
    assert s.service.years == [2020, 2021]
    assert list(first.columns) == list(RECORD_COLUMNS)
    pd.testing.assert_frame_equal(first, second)
    assert s.get_cache_path(2020).exists()


def test_current_and_last_year_are_always_refetched(sightings):
    s = sightings(2024, 2026)
    s.fetch(current_year=2026)
    s.fetch(current_year=2026)
    assert s.service.years == [2024, 2025, 2026, 2025, 2026]
    assert s.get_cache_path(2024).exists()
    assert not s.get_cache_path(2025).exists() and not s.get_cache_path(2026).exists()


def test_cache_file_name_names_the_taxon_year_and_box(sightings):
    assert sightings(2020, 2020).get_cache_path(2020).name == "sightings_359_2020_29.3_-11.8_40.5_-0.95.parquet"


def test_no_cache_without_cache_dir(config):
    assert Sightings(config).get_cache_path(2024) is None


def test_stale_cache_file_is_deleted_and_fetched_again(sightings):
    s = sightings(2020, 2020)
    path = s.get_cache_path(2020)
    path.parent.mkdir(parents=True, exist_ok=True)
    pd.DataFrame([{"gbifID": "1", "year": 2020}]).to_parquet(path, index=False)

    records = s.fetch(current_year=2030)

    assert s.service.years == [2020]
    assert "coordinateUncertaintyInMeters" in records
    assert list(pd.read_parquet(path).columns) == list(RECORD_COLUMNS)
