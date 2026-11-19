import json
from dataclasses import replace
from datetime import date

import numpy as np
import pandas as pd
import pytest

from jobs.shared import outputs
from jobs.sightings import refresh
from jobs.sightings.models import SightingsGridSettings


@pytest.fixture(scope="module")
def sightings(tmp_path_factory, config):
    rng = np.random.default_rng(1)
    records = pd.DataFrame(
        {
            "decimalLongitude": rng.uniform(34.5, 34.9, 3_000),
            "decimalLatitude": rng.uniform(-2.9, -2.5, 3_000),
            "coordinateUncertaintyInMeters": rng.choice([np.nan, 10.0, 3_000.0], 3_000),
            "speciesKey": rng.choice(list(config.species.values()) + [999], 3_000),
        }
    )
    config = replace(config, bounds=(34.5, -2.9, 34.9, -2.5), start_year=2017, end_year=2026)
    settings = SightingsGridSettings(sigmas_m=(5_000, 1_000), tile_cells=32)
    data_dir = tmp_path_factory.mktemp("data")
    manifest = refresh.run_sightings(records, date(2026, 11, 15), config, data_dir, settings)
    return records, manifest, data_dir / "sightings" / "2026-11-15", config.species


def test_sightings_manifest_lists_layers_and_tiles(sightings):
    records, manifest, run_dir, species = sightings
    layers = {layer["sigma_m"]: layer for layer in manifest["layers"]}

    assert manifest["years"] == [2017, 2026] and manifest["date"] == "2026-11-15"
    assert manifest["species"]["lion"]["records"] == int((records["speciesKey"] == species["lion"]).sum())
    assert layers[5_000]["records"] == 3_000 and layers[5_000]["max_uncertainty_m"] is None
    assert layers[1_000]["max_uncertainty_m"] == 1_000
    assert sum(layers[1_000]["species_records"].values()) < sum(layers[5_000]["species_records"].values())
    assert layers[1_000]["records"] == int((records["coordinateUncertaintyInMeters"] != 3_000.0).sum())
    assert len(layers[1_000]["tiles"]) > 1
    expected = {
        refresh.tile_file(name, sigma, t["tx"], t["ty"])
        for sigma, layer in layers.items()
        for t in layer["tiles"]
        for name in species
    }
    assert set(manifest["files"]) == expected
    assert all((run_dir / f).exists() for f in manifest["files"])


def test_sightings_tile_payload(sightings):
    _, manifest, run_dir, species = sightings
    tile_ref = manifest["layers"][1]["tiles"][0]
    lion = json.loads((run_dir / refresh.tile_file("lion", 1_000, tile_ref["tx"], tile_ref["ty"])).read_text())
    assert len(lion["share"]) == lion["nx"] * lion["ny"] == len(lion["n"]) and lion["nx"] <= 32
    assert lion["crs"] == "EPSG:32736" and lion["cell_m"] == 500
    assert lion["bounds_wgs84"] == tile_ref["bounds_wgs84"]
    assert outputs.read_latest(run_dir.parent)["run"] == "2026-11-15"
