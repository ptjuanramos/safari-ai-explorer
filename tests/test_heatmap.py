import json

import numpy as np
import pandas as pd
import pytest

from safari_explorer.config import SPECIES, Config
from safari_explorer.heatmap import SightingsHeat, sightings_heat_map, species_scale_max, to_utm


@pytest.fixture(scope="module")
def records():
    rng = np.random.default_rng(0)
    n = 2_000
    lon = rng.uniform(34.5, 35.5, n)
    lat = rng.uniform(-3.0, -2.0, n)
    buffalo_area = (lon < 34.8) & (lat < -2.7)
    species = np.where(buffalo_area & (rng.uniform(size=n) < 0.6), SPECIES["buffalo"],
                       rng.choice([SPECIES["lion"], SPECIES["elephant"], 999], n))
    df = pd.DataFrame({"decimalLongitude": lon, "decimalLatitude": lat, "speciesKey": species})
    return pd.concat([df, pd.DataFrame({"decimalLongitude": [np.nan], "decimalLatitude": [np.nan], "speciesKey": [999]})],
                     ignore_index=True)


@pytest.fixture(scope="module")
def payload(records):
    heat = sightings_heat_map(records, SPECIES, Config().bounds)
    layer = next(c for c in heat._children.values() if isinstance(c, SightingsHeat))
    return json.loads(layer.data)


def test_records_without_coordinates_dropped(records, payload):
    assert len(payload["x"]) == records["decimalLongitude"].notna().sum()


def test_average_share_per_species(records, payload):
    with_coords = records.dropna(subset=["decimalLongitude"])
    for i, key in enumerate(SPECIES.values()):
        assert payload["p0"][i] == pytest.approx((with_coords["speciesKey"] == key).mean())
        assert payload["s"].count(i) == (with_coords["speciesKey"] == key).sum()


def test_scale_top_above_average_for_concentrated_species(payload):
    i = list(SPECIES).index("buffalo")
    assert payload["p0"][i] < payload["vmax"][i] <= 1


def test_coordinates_in_utm_meters(records, payload):
    x, y = to_utm.transform(records["decimalLongitude"].iloc[0], records["decimalLatitude"].iloc[0])
    assert payload["x"][0] == round(x) and payload["y"][0] == round(y)


def test_scale_max_is_share_quantile(records):
    df = records.dropna(subset=["decimalLongitude"]).copy()
    df["x"], df["y"] = to_utm.transform(df["decimalLongitude"].values, df["decimalLatitude"].values)
    assert 0 < species_scale_max(df, SPECIES["buffalo"]) <= 1
