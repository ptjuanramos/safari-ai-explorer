import pandas as pd
import pytest
import requests

from safari_explorer import gbif
from safari_explorer.config import Config


def test_fetch_all_follows_pages(monkeypatch):
    offsets = []

    def fake_page(params):
        offsets.append(params["offset"])
        last = params["offset"] >= 2 * gbif.PAGE_SIZE
        return {"results": [{"gbifID": params["offset"]}], "endOfRecords": last}

    monkeypatch.setattr(gbif, "get_page", fake_page)
    df = gbif.fetch_all({})
    assert offsets == [0, gbif.PAGE_SIZE, 2 * gbif.PAGE_SIZE]
    assert len(df) == 3


def test_get_page_retries_then_fails(monkeypatch):
    calls = []

    def failing_get(*args, **kwargs):
        calls.append(1)
        raise requests.ConnectionError("down")

    monkeypatch.setattr(gbif.requests, "get", failing_get)
    monkeypatch.setattr(gbif.time, "sleep", lambda s: None)
    with pytest.raises(RuntimeError):
        gbif.get_page({"offset": 0}, retries=3)
    assert len(calls) == 3


def test_fetch_mammal_records_uses_cache(tmp_path, monkeypatch):
    config = Config(cache_dir=str(tmp_path), start_year=2020, end_year=2021)
    queried_years = []

    def fake_fetch_all(params):
        queried_years.append(params["year"])
        return pd.DataFrame([{"gbifID": 1, "decimalLongitude": 34.8, "decimalLatitude": -2.4,
                              "year": params["year"], "speciesKey": 2441034, "species": "Syncerus caffer"}])

    monkeypatch.setattr(gbif, "fetch_all", fake_fetch_all)
    first = gbif.fetch_mammal_records(config)
    second = gbif.fetch_mammal_records(config)
    assert queried_years == [2020, 2021]
    assert list(first.columns) == gbif.COLUMNS
    pd.testing.assert_frame_equal(first, second)


def test_no_cache_file_without_cache_dir():
    assert gbif.cache_path(Config()) is None
