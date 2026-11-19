import numpy as np
import pandas as pd
import pytest

from jobs.shared.geo import Geo


def test_with_utm_xy_drops_records_without_coordinates_and_adds_meters():
    records = pd.DataFrame({"decimalLongitude": [35.0, np.nan], "decimalLatitude": [-2.5, -2.5]})
    located = Geo.with_utm_xy(records)
    x, y = Geo.TO_UTM.transform(35.0, -2.5)
    assert len(located) == 1
    assert located["x"].iloc[0] == pytest.approx(x) and located["y"].iloc[0] == pytest.approx(y)
    assert 600_000 < x < 800_000 and 9_600_000 < y < 9_800_000


def test_bounds_round_trip():
    bounds = (34.5, -3.0, 35.5, -2.0)
    back = Geo.utm_to_wgs84_bounds(*Geo.wgs84_to_utm_bounds(bounds))
    assert back[0] <= bounds[0] and back[1] <= bounds[1] and back[2] >= bounds[2] and back[3] >= bounds[3]
    assert np.allclose(back, bounds, atol=0.01)
