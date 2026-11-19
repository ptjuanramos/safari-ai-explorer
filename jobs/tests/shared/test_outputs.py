import json

import numpy as np

from jobs.shared import outputs


def test_slug():
    assert outputs.slug("Tarangire National Park, Tanzania") == "tarangire-national-park-tanzania"


def test_nan_becomes_null():
    assert outputs.to_jsonable({"a": [1.0, float("nan")], "b": np.array([np.inf, 2.0]), "c": np.float64(3)}) == {
        "a": [1.0, None],
        "b": [None, 2.0],
        "c": 3.0,
    }


def test_write_run_writes_files_then_manifest(tmp_path, monkeypatch):
    order = []
    real_write = outputs.write_text_atomic

    def tracking_write(path, text):
        order.append(path.name)
        real_write(path, text)

    monkeypatch.setattr(outputs, "write_text_atomic", tracking_write)
    run_dir = tmp_path / "road-risk" / "park" / "2026-11-15"
    manifest = outputs.write_run(run_dir, {"edges.geojson": "{}", "extra.json": {"x": float("nan")}}, {"job": "test"})

    assert order == ["edges.geojson", "extra.json", "manifest.json"]
    assert manifest["files"] == ["edges.geojson", "extra.json"]
    assert json.loads((run_dir / "extra.json").read_text()) == {"x": None}
    assert outputs.run_exists(run_dir)
    assert not list(run_dir.glob("*.tmp"))


def test_run_without_manifest_does_not_exist(tmp_path):
    (tmp_path / "2026-11-15").mkdir()
    (tmp_path / "2026-11-15" / "edges.geojson").write_text("{}")
    assert not outputs.run_exists(tmp_path / "2026-11-15")


def test_latest_points_at_newest_run(tmp_path):
    assert outputs.read_latest(tmp_path) is None
    assert outputs.update_latest(tmp_path, "2026-11-14")
    assert outputs.update_latest(tmp_path, "2026-11-15")
    assert outputs.read_latest(tmp_path)["run"] == "2026-11-15"
    assert outputs.read_latest(tmp_path)["manifest"] == "2026-11-15/manifest.json"


def test_latest_never_moves_backwards_when_asked(tmp_path):
    outputs.update_latest(tmp_path, "2026-11-15")
    assert not outputs.update_latest(tmp_path, "2024-04-30", only_if_newer=True)
    assert outputs.read_latest(tmp_path)["run"] == "2026-11-15"
    assert outputs.update_latest(tmp_path, "2026-11-16", only_if_newer=True)
    assert outputs.read_latest(tmp_path)["run"] == "2026-11-16"


def test_state_only_moves_forward(tmp_path):
    assert outputs.read_state(tmp_path) is None
    assert outputs.advance_state(tmp_path, "2026-11-13", "road-risk-backfill")
    assert not outputs.advance_state(tmp_path, "2026-11-10", "road-risk-refresh")
    assert not outputs.advance_state(tmp_path, "2026-11-13", "road-risk-refresh")
    state = outputs.read_state(tmp_path)
    assert state["complete_through"] == "2026-11-13" and state["updated_by"] == "road-risk-backfill"
