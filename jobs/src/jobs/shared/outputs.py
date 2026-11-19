import json
import math
import os
import re
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

MANIFEST = "manifest.json"
LATEST = "latest.json"


def slug(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def to_jsonable(value):
    if isinstance(value, dict):
        return {k: to_jsonable(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [to_jsonable(v) for v in value]
    if isinstance(value, np.ndarray):
        return to_jsonable(value.tolist())
    if isinstance(value, np.generic):
        value = value.item()
    if isinstance(value, float) and not math.isfinite(value):
        return None
    return value


def write_text_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(text, encoding="utf-8")
    os.replace(tmp, path)


def write_json(path: Path, obj) -> None:
    write_text_atomic(path, json.dumps(to_jsonable(obj), allow_nan=False, separators=(",", ":")))


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def run_exists(run_dir: Path) -> bool:
    return (run_dir / MANIFEST).exists()


def write_file(run_dir: Path, name: str, content: str | dict) -> str:
    if isinstance(content, str):
        write_text_atomic(run_dir / name, content)
    else:
        write_json(run_dir / name, content)
    return name


def write_manifest(run_dir: Path, files: list[str], manifest: dict) -> dict:
    manifest = {**manifest, "files": sorted(files), "written_at": utc_now()}
    write_json(run_dir / MANIFEST, manifest)
    return manifest


def write_run(run_dir: Path, files: dict[str, str | dict], manifest: dict) -> dict:
    return write_manifest(run_dir, [write_file(run_dir, name, content) for name, content in files.items()], manifest)


def read_latest(job_dir: Path) -> dict | None:
    path = job_dir / LATEST
    return read_json(path) if path.exists() else None


def update_latest(job_dir: Path, run_id: str, only_if_newer: bool = False) -> bool:
    current = read_latest(job_dir)
    if only_if_newer and current is not None and current["run"] >= run_id:
        return False
    write_json(job_dir / LATEST, {"run": run_id, "manifest": f"{run_id}/{MANIFEST}", "updated_at": utc_now()})
    return True


STATE = "state.json"


def read_state(job_dir: Path) -> dict | None:
    path = job_dir / STATE
    return read_json(path) if path.exists() else None


def advance_state(job_dir: Path, day: str, job: str) -> bool:
    current = read_state(job_dir)
    if current is not None and current["complete_through"] >= day:
        return False
    write_json(job_dir / STATE, {"complete_through": day, "updated_by": job, "updated_at": utc_now()})
    return True
