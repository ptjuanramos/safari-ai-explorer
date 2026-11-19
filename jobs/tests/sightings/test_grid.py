import numpy as np
import pandas as pd
import pytest

from jobs.shared.geo import Geo
from jobs.sightings.grid import ShareTileBuilder, TileGrid
from jobs.sightings.models import SightingsGridSettings

BOUNDS = (34.5, -3.0, 35.5, -2.0)
ONE_TILE = SightingsGridSettings(tile_cells=10_000)


@pytest.fixture(scope="module")
def records(species):
    rng = np.random.default_rng(0)
    n = 2_000
    lon = rng.uniform(34.5, 35.5, n)
    lat = rng.uniform(-3.0, -2.0, n)
    buffalo_area = (lon < 34.8) & (lat < -2.7)
    keys = np.where(
        buffalo_area & (rng.uniform(size=n) < 0.6),
        species["buffalo"],
        rng.choice([species["lion"], species["elephant"], 999], n),
    )
    located = pd.DataFrame({"decimalLongitude": lon, "decimalLatitude": lat, "speciesKey": keys})
    missing = pd.DataFrame({"decimalLongitude": [np.nan], "decimalLatitude": [np.nan], "speciesKey": [999]})
    return pd.concat([located, missing], ignore_index=True)


@pytest.fixture(scope="module")
def whole(records, species):
    return {t.species: t for t in ShareTileBuilder(species, BOUNDS, ONE_TILE).build(records, 5_000)}


def cell_of(tile, lon, lat):
    x, y = Geo.TO_UTM.transform(lon, lat)
    return int(round((y - tile.y0) / tile.cell_m)), int(round((x - tile.x0) / tile.cell_m))


def test_tile_grid_covers_bounds_with_aligned_cell_centres():
    grid = TileGrid.covering(BOUNDS, 2_500, 256)
    minx, miny, maxx, maxy = Geo.wgs84_to_utm_bounds(BOUNDS)
    assert grid.xs[0] - 1_250 <= minx and grid.xs[-1] + 1_250 >= maxx
    assert grid.ys[0] - 1_250 <= miny and grid.ys[-1] + 1_250 >= maxy
    assert np.allclose(np.diff(grid.xs), 2_500) and np.allclose((grid.xs - 1_250) % 2_500, 0)


def test_tile_grid_cuts_every_cell_into_exactly_one_tile():
    grid = TileGrid(np.arange(10.0), np.arange(7.0), tile_cells=4)
    tiles = list(grid.tiles())
    assert [(tx, ty) for tx, ty, _, _ in tiles] == [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)]
    assert sum(len(xs) * len(ys) for _, _, xs, ys in tiles) == 70
    assert TileGrid.tile_count(10, 4) == 3


def test_average_share_per_species(records, whole, species):
    located = records.dropna(subset=["decimalLongitude"])
    for name, key in species.items():
        assert whole[name].p0 == pytest.approx((located["speciesKey"] == key).mean())


def test_share_uses_beta_prior_and_range(species):
    records = pd.DataFrame(
        {
            "decimalLongitude": [35.0] * 6 + [35.3] * 4,
            "decimalLatitude": [-2.5] * 10,
            "speciesKey": [species["lion"]] * 6 + [999] * 4,
        }
    )
    settings = SightingsGridSettings(prior_records=10, min_records=1, tile_cells=10_000)
    lion = next(ShareTileBuilder({"lion": species["lion"]}, BOUNDS, settings).build(records, 1_000))
    row, col = cell_of(lion, 35.0, -2.5)
    n, share = lion.n[row, col], lion.share[row, col]
    k = share * (n + 10) - 10 * lion.p0
    assert lion.p0 == pytest.approx(0.6)
    assert k == pytest.approx(n, rel=1e-6)
    assert lion.p0 < share < 1
    assert lion.lo[row, col] <= share <= lion.hi[row, col]


def test_thin_cells_are_empty(whole):
    buffalo = whole["buffalo"]
    empty = buffalo.n < SightingsGridSettings().min_records
    assert np.isnan(buffalo.share[empty]).all()
    filled = ~empty
    assert np.all(buffalo.lo[filled] <= buffalo.share[filled])
    assert np.all(buffalo.share[filled] <= buffalo.hi[filled])
    assert not np.isnan(buffalo.share[filled]).any()


def test_concentrated_species_peaks_in_its_area(whole):
    buffalo = whole["buffalo"]
    row, col = cell_of(buffalo, 34.65, -2.85)
    assert buffalo.share[row, col] > 2 * buffalo.p0


def test_tiles_match_the_whole_grid(records, whole, species):
    size = 16
    lion = whole["lion"]
    builder = ShareTileBuilder(species, BOUNDS, SightingsGridSettings(tile_cells=size))
    tiles = [t for t in builder.build(records, 5_000) if t.species == "lion"]
    rows, cols = lion.shape
    expected = {
        (tx, ty) for tx in range(TileGrid.tile_count(cols, size)) for ty in range(TileGrid.tile_count(rows, size))
    }
    assert {(t.tx, t.ty) for t in tiles} == expected
    for t in tiles:
        r = slice(t.ty * size, t.ty * size + t.shape[0])
        c = slice(t.tx * size, t.tx * size + t.shape[1])
        assert t.x0 == pytest.approx(lion.x0 + t.tx * size * lion.cell_m)
        np.testing.assert_allclose(t.n, lion.n[r, c])
        np.testing.assert_allclose(t.share, lion.share[r, c])


def test_tiles_far_from_records_are_skipped(species):
    rng = np.random.default_rng(2)
    records = pd.DataFrame(
        {
            "decimalLongitude": rng.uniform(34.55, 34.6, 50),
            "decimalLatitude": rng.uniform(-2.95, -2.9, 50),
            "speciesKey": rng.choice([species["lion"], 999], 50),
        }
    )
    settings = SightingsGridSettings(tile_cells=32)
    tiles = list(ShareTileBuilder({"lion": species["lion"]}, BOUNDS, settings).build(records, 1_000))
    grid = TileGrid.covering(BOUNDS, 500, 32)
    assert 0 < len(tiles) < TileGrid.tile_count(len(grid.xs), 32) * TileGrid.tile_count(len(grid.ys), 32)
    assert all(t.tx == 0 and t.ty == 0 for t in tiles)
    assert all(t.bounds_wgs84()[0] <= 34.55 and t.bounds_wgs84()[1] <= -2.95 for t in tiles)


def test_fine_layers_drop_imprecise_records(species):
    records = pd.DataFrame(
        {
            "decimalLongitude": [35.0] * 10,
            "decimalLatitude": [-2.5] * 10,
            "speciesKey": [species["lion"]] * 5 + [999] * 5,
            "coordinateUncertaintyInMeters": [20_000.0] * 5 + [10.0] * 5,
        }
    )
    builder = ShareTileBuilder(
        {"lion": species["lion"]}, BOUNDS, SightingsGridSettings(min_records=1, tile_cells=10_000)
    )
    fine = next(builder.build(records, 1_000))
    coarse = next(builder.build(records, 20_000))
    assert fine.p0 == 0 and coarse.p0 == pytest.approx(0.5)
    assert 4.5 < np.nanmax(fine.n) <= 5
