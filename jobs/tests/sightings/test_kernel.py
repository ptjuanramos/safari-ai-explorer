import numpy as np
import pandas as pd
import pytest

from jobs.shared.geo import Geo
from jobs.sightings.kernel import KernelShare


def test_weighted_counts_match_hand_computed_weights():
    xy = np.array([[0.0, 0.0], [1_000.0, 0.0], [10_000.0, 0.0]])
    codes = np.array([0, 1, -1])
    n, k = KernelShare.weighted_counts(xy, codes, 2, np.array([[0.0, 0.0]]), sigma_m=1_000)
    assert n[0] == pytest.approx(1 + np.exp(-0.5))
    assert k[0].tolist() == pytest.approx([1.0, np.exp(-0.5)])


def test_beta_share_pulls_thin_cells_toward_the_average():
    assert KernelShare.beta_share(np.array([0.0]), np.array([0.0]), 0.2, 10)[0] == pytest.approx(0.2)
    assert KernelShare.beta_share(np.array([6.0]), np.array([6.0]), 0.2, 10)[0] == pytest.approx((6 + 2) / 16)
    assert KernelShare.beta_share(np.array([900.0]), np.array([1000.0]), 0.2, 10)[0] == pytest.approx(902 / 1010)


def test_share_range_contains_the_share_and_narrows_with_records():
    share = np.array([0.3, 0.3])
    lo, hi = KernelShare.share_range(share, np.array([10.0, 1000.0]), 10)
    assert np.all(lo <= share) and np.all(share <= hi)
    assert hi[1] - lo[1] < hi[0] - lo[0]
    assert KernelShare.share_range(np.array([0.99]), np.array([0.0]), 1)[1][0] == 1


def test_species_average_and_scale_max():
    rng = np.random.default_rng(0)
    lon, lat = rng.uniform(34.5, 35.5, 2_000), rng.uniform(-3.0, -2.0, 2_000)
    in_area = (lon < 34.8) & (lat < -2.7) & (rng.uniform(size=2_000) < 0.6)
    records = Geo.with_utm_xy(
        pd.DataFrame({"decimalLongitude": lon, "decimalLatitude": lat, "speciesKey": np.where(in_area, 1, 999)})
    )
    p0 = KernelShare.species_average(records, 1)
    assert p0 == pytest.approx(in_area.mean())
    assert p0 < KernelShare.scale_max(records, 1) <= 1
