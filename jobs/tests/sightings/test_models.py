import numpy as np
import pytest

from jobs.sightings.models import ShareTile, SightingsGridSettings


def test_default_settings_cover_zooms_9_to_14():
    s = SightingsGridSettings()
    assert s.sigmas_m == (10_000, 5_000, 2_000, 1_000, 500, 250)
    assert (s.prior_records, s.min_records, s.confidence_records, s.tile_cells) == (10, 5, 10, 256)
    assert s.uncertainty_filter_max_sigma_m == 2_000
    assert s.cell_m(5_000) == 2_500


def test_tile_bounds_cover_its_cells():
    cells = np.zeros((2, 3))
    tile = ShareTile("lion", 1_000, 500, 0, 0, 750_000.0, 9_700_000.0, 0.1, cells, cells, cells, cells)
    west, south, east, north = tile.bounds_wgs84()
    assert tile.shape == (2, 3)
    assert west < east and south < north
    assert (east - west) * 111_320 == pytest.approx(3 * 500, rel=0.01)
