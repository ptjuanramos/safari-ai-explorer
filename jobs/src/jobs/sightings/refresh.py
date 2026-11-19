from dataclasses import asdict
from datetime import date
from pathlib import Path

import numpy as np
import pandas as pd

from jobs.shared import cli
from jobs.shared.crs import UTM
from jobs.shared.geo import Geo
from jobs.shared.outputs import update_latest, write_file, write_manifest
from jobs.sightings.config import SightingsConfig
from jobs.sightings.grid import ShareTileBuilder, TileGrid
from jobs.sightings.kernel import KernelShare
from jobs.sightings.models import ShareTile, SightingsGridSettings
from jobs.sightings.records import LayerRecords
from jobs.sightings.sightings import Sightings

JOB = "sightings"
SCALE_SIGMA_M = 5_000
MEANING = (
    "Share of logged mammal records that were this species, around each cell. "
    "It shows where visitors see the animal, not everywhere it lives."
)


def job_dir(data_dir: str | Path) -> Path:
    return Path(data_dir) / JOB


def tile_file(species: str, sigma_m: float, tx: int, ty: int) -> str:
    return f"{species}/{int(sigma_m)}m/{tx}_{ty}.json"


def rounded(values: np.ndarray, digits: int) -> list:
    return np.round(values.ravel(), digits).tolist()


def tile_payload(tile: ShareTile, settings: SightingsGridSettings) -> dict:
    rows, cols = tile.shape
    return {
        "species": tile.species,
        "crs": UTM,
        "sigma_m": tile.sigma_m,
        "cell_m": tile.cell_m,
        "tx": tile.tx,
        "ty": tile.ty,
        "x0": tile.x0,
        "y0": tile.y0,
        "nx": cols,
        "ny": rows,
        "bounds_wgs84": list(tile.bounds_wgs84()),
        "p0": tile.p0,
        "confidence_records": settings.confidence_records,
        "share": rounded(tile.share, 4),
        "n": rounded(tile.n, 2),
        "lo": rounded(tile.lo, 4),
        "hi": rounded(tile.hi, 4),
    }


def colour_scale(records: pd.DataFrame, species: dict[str, int], settings: SightingsGridSettings) -> dict:
    scale_records = LayerRecords.for_layer(records, SCALE_SIGMA_M, settings)
    return {
        name: {
            "p0": KernelShare.species_average(scale_records, key),
            "vmax": KernelShare.scale_max(scale_records, key),
        }
        for name, key in species.items()
    }


def run_layer(
    records: pd.DataFrame,
    config: SightingsConfig,
    run_dir: Path,
    sigma_m: float,
    settings: SightingsGridSettings,
) -> tuple[dict, list[str]]:
    builder = ShareTileBuilder(config.species, config.bounds, settings)
    files, tiles = [], {}
    for tile in builder.build(records, sigma_m):
        name = tile_file(tile.species, sigma_m, tile.tx, tile.ty)
        files.append(write_file(run_dir, name, tile_payload(tile, settings)))
        tiles.setdefault((tile.tx, tile.ty), list(tile.bounds_wgs84()))

    used = LayerRecords.for_layer(records, sigma_m, settings)
    grid = TileGrid.covering(config.bounds, settings.cell_m(sigma_m), settings.tile_cells)
    layer = {
        "sigma_m": sigma_m,
        "cell_m": settings.cell_m(sigma_m),
        "tile_cells": settings.tile_cells,
        "max_uncertainty_m": settings.max_uncertainty_m(sigma_m),
        "records": len(used),
        "species_records": {name: int((used["speciesKey"] == key).sum()) for name, key in config.species.items()},
        "grid": {"x0": float(grid.xs[0]), "y0": float(grid.ys[0]), "nx": len(grid.xs), "ny": len(grid.ys)},
        "species_p0": {name: KernelShare.species_average(used, key) for name, key in config.species.items()},
        "tiles": [{"tx": tx, "ty": ty, "bounds_wgs84": bounds} for (tx, ty), bounds in sorted(tiles.items())],
    }
    return layer, files


def run_sightings(
    records: pd.DataFrame,
    run_date: date,
    config: SightingsConfig,
    data_dir: str | Path,
    settings: SightingsGridSettings = SightingsGridSettings(),
) -> dict:
    located = Geo.with_utm_xy(records)
    run_dir = job_dir(data_dir) / run_date.isoformat()
    scale = colour_scale(located, config.species, settings)

    layers, files = [], []
    for sigma_m in settings.sigmas_m:
        layer, layer_files = run_layer(located, config, run_dir, sigma_m, settings)
        layers.append(layer)
        files.extend(layer_files)
        print(f"sigma {int(sigma_m)} m: {layer['records']} records, {len(layer['tiles'])} tiles")

    manifest = {
        "job": "sightings-refresh",
        "date": run_date.isoformat(),
        "years": [config.start_year, config.end_year],
        "bounds_wgs84": list(config.bounds),
        "crs": UTM,
        "records": len(located),
        "settings": asdict(settings),
        "layers": layers,
        "species": {
            name: {"species_key": key, "records": int((located["speciesKey"] == key).sum()), **scale[name]}
            for name, key in config.species.items()
        },
        "meaning": MEANING,
    }

    manifest = write_manifest(run_dir, files, manifest)
    update_latest(job_dir(data_dir), run_date.isoformat())
    return manifest


def main(argv: list[str] | None = None) -> int:
    p = cli.parser("Sightings share tiles per kernel width from all GBIF records up to today.")
    p.add_argument("--start-year", type=int)
    args = p.parse_args(argv)

    today = cli.today_utc()
    overrides = {"cache_dir": args.cache_dir, "end_year": today.year}
    if args.start_year:
        overrides["start_year"] = args.start_year
    config = SightingsConfig.load(args.settings, **overrides)

    records = Sightings(config).fetch(current_year=today.year)
    manifest = run_sightings(records, today, config, args.data_dir)
    print(f"{today}: {manifest['records']} records, {len(manifest['files'])} tiles")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
