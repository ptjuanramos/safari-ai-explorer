import pandas as pd
import pytest
import requests

from jobs.sightings import service as service_module
from jobs.sightings.models import RECORD_COLUMNS
from jobs.sightings.service import SightingsService


def test_fetch_all_follows_pages(monkeypatch):
    service = SightingsService(page_size=2)
    offsets = []

    def fake_page(self, params):
        offsets.append(params["offset"])
        return {"results": [{"gbifID": params["offset"]}], "endOfRecords": params["offset"] >= 4}

    monkeypatch.setattr(SightingsService, "get_page", fake_page)
    records = service.fetch_all({})
    assert offsets == [0, 2, 4]
    assert len(records) == 3


def test_get_page_retries_then_fails(monkeypatch):
    calls = []

    def failing_get(*args, **kwargs):
        calls.append(kwargs["timeout"])
        raise requests.ConnectionError("down")

    monkeypatch.setattr(service_module.requests, "get", failing_get)
    monkeypatch.setattr(service_module.time, "sleep", lambda s: None)
    with pytest.raises(RuntimeError):
        SightingsService(retries=3, timeout_s=9).get_page({"offset": 0})
    assert calls == [9, 9, 9]


class FakeStreamedResponse:
    """A streamed answer that hands out its body in the given chunks."""

    def __init__(self, chunks, on_chunk=lambda: None):
        self.chunks, self.on_chunk = chunks, on_chunk

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def raise_for_status(self):
        pass

    def iter_content(self, chunk_size):
        for chunk in self.chunks:
            self.on_chunk()
            yield chunk


def test_get_page_joins_the_streamed_chunks(monkeypatch):
    chunks = [b'{"results": [{"gbifID": 1}],', b' "endOfRecords": true}']
    monkeypatch.setattr(service_module.requests, "get", lambda *a, **kw: FakeStreamedResponse(chunks))

    page = SightingsService().get_page({"offset": 0})

    assert page == {"results": [{"gbifID": 1}], "endOfRecords": True}


def test_get_page_gives_up_on_a_trickle_past_the_deadline(monkeypatch):
    # Each chunk arrives 50 s after the last: every wait is under the 120 s timeout, but the whole page isn't.
    clock = {"now": 0.0}

    def tick():
        clock["now"] += 50

    def trickling_get(*args, **kwargs):
        return FakeStreamedResponse([b"{"] * 10, on_chunk=tick)

    monkeypatch.setattr(service_module.time, "monotonic", lambda: clock["now"])
    monkeypatch.setattr(service_module.time, "sleep", lambda s: None)
    monkeypatch.setattr(service_module.requests, "get", trickling_get)

    with pytest.raises(RuntimeError):
        SightingsService(retries=2, timeout_s=120).get_page({"offset": 0})
    # Three chunks (150 s) pass the deadline on each of the two attempts.
    assert clock["now"] == 2 * 150


def test_fetch_year_asks_for_the_taxon_in_the_box_and_returns_record_columns(monkeypatch):
    seen = []

    def fake_fetch_all(self, params):
        seen.append(params)
        return pd.DataFrame([{"gbifID": 1, "decimalLongitude": 34.8, "extra": "x"}])

    monkeypatch.setattr(SightingsService, "fetch_all", fake_fetch_all)
    monkeypatch.setattr(SightingsService, "count_records", lambda self, params: 1)
    records = SightingsService().fetch_year(359, (34.0, -3.6, 36.0, -1.6), 2024)

    assert seen == [
        {
            "classKey": 359,
            "decimalLongitude": "34.0,36.0",
            "decimalLatitude": "-3.6,-1.6",
            "hasCoordinate": "true",
            "hasGeospatialIssue": "false",
            "year": 2024,
        }
    ]
    assert list(records.columns) == list(RECORD_COLUMNS)
    assert records["gbifID"].tolist() == ["1"]


def test_halves_cut_across_the_longer_side():
    assert SightingsService.halves((30.0, -10.0, 40.0, -6.0)) == [(30.0, -10.0, 35.0, -6.0), (35.0, -10.0, 40.0, -6.0)]
    assert SightingsService.halves((30.0, -10.0, 32.0, -2.0)) == [(30.0, -10.0, 32.0, -6.0), (30.0, -6.0, 32.0, -2.0)]


def test_boxes_under_limit_splits_until_each_part_fits_and_drops_empty_parts(monkeypatch):
    # 12 records on the west half, none on the east: the square west half is split once more, across its longitude.
    counts = {
        (0.0, 0.0, 4.0, 2.0): 12,
        (0.0, 0.0, 2.0, 2.0): 12,
        (2.0, 0.0, 4.0, 2.0): 0,
        (0.0, 0.0, 1.0, 2.0): 7,
        (1.0, 0.0, 2.0, 2.0): 5,
    }

    def fake_count(self, params):
        west, east = map(float, params["decimalLongitude"].split(","))
        south, north = map(float, params["decimalLatitude"].split(","))
        return counts[(west, south, east, north)]

    monkeypatch.setattr(SightingsService, "count_records", fake_count)
    boxes = SightingsService(max_records=10).boxes_under_limit(359, (0.0, 0.0, 4.0, 2.0), 2019)

    assert boxes == [(0.0, 0.0, 1.0, 2.0), (1.0, 0.0, 2.0, 2.0)]


def test_boxes_under_limit_gives_up_on_a_box_too_small_to_split(monkeypatch):
    monkeypatch.setattr(SightingsService, "count_records", lambda self, params: 50)
    with pytest.raises(RuntimeError):
        SightingsService(max_records=10, min_box_deg=1.0).boxes_under_limit(359, (0.0, 0.0, 0.5, 0.5), 2019)


def test_fetch_year_joins_the_parts_and_drops_records_on_a_split_line(monkeypatch):
    halves = SightingsService.halves((0.0, 0.0, 2.0, 1.0))
    monkeypatch.setattr(SightingsService, "boxes_under_limit", lambda self, taxon, bounds, year: halves)

    def fake_fetch_all(self, params):
        west = params["decimalLongitude"].split(",")[0]
        # Record 2 lies on the line at 1.0°E, so both halves return it.
        return pd.DataFrame([{"gbifID": 1}, {"gbifID": 2}] if west == "0.0" else [{"gbifID": 2}, {"gbifID": 3}])

    monkeypatch.setattr(SightingsService, "fetch_all", fake_fetch_all)
    records = SightingsService().fetch_year(359, (0.0, 0.0, 2.0, 1.0), 2019)

    assert records["gbifID"].tolist() == ["1", "2", "3"]


def test_count_records_asks_for_an_empty_page(monkeypatch):
    seen = []

    def fake_page(self, params):
        seen.append(params)
        return {"count": 15312, "results": [], "endOfRecords": False}

    monkeypatch.setattr(SightingsService, "get_page", fake_page)
    assert SightingsService().count_records({"year": 2019}) == 15312
    assert seen == [{"year": 2019, "offset": 0, "limit": 0}]
