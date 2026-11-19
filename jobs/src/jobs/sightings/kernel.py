from typing import ClassVar

import numpy as np
import pandas as pd
from scipy.spatial import cKDTree


class KernelShare:
    """
    The heat-map calculation.

    Around each cell centre, every record gets a Gaussian weight by its distance. Then:
        n = sum of the weights of all records (how much was logged there: visitor effort)
        k = sum of the weights of one species' records
        share = (k + prior * p0) / (n + prior)   (effort-adjusted share with a beta prior toward the average p0)
    """

    REACH_SIGMAS: ClassVar[float] = 3
    Z_95: ClassVar[float] = 1.96

    @staticmethod
    def gaussian_weights(distance_m: np.ndarray, sigma_m: float) -> np.ndarray:
        return np.exp(-(distance_m**2) / (2 * sigma_m**2))

    @classmethod
    def reach_m(cls, sigma_m: float) -> float:
        """Records farther than this from a cell are ignored: their weight is about 1% or less."""
        return cls.REACH_SIGMAS * sigma_m

    @classmethod
    def weighted_counts(
        cls,
        xy: np.ndarray,
        species_codes: np.ndarray,
        n_species: int,
        centres: np.ndarray,
        sigma_m: float,
    ) -> tuple[np.ndarray, np.ndarray]:
        """
        Gaussian-weighted record counts around each centre.

        :param xy: Record positions in UTM meters, shape (records, 2).
        :param species_codes: Per record, the index of its species in the species list, or -1 for any other species.
        :param n_species: Length of the species list.
        :param centres: Cell centres in UTM meters, shape (cells, 2).
        :param sigma_m: Kernel width in meters.
        :return: n with shape (cells,) for all records, and k with shape (cells, n_species) per species.
        """
        pairs = cKDTree(centres).sparse_distance_matrix(
            cKDTree(xy),
            max_distance=cls.reach_m(sigma_m),
            output_type="ndarray",
        )

        cell, record, weight = pairs["i"], pairs["j"], cls.gaussian_weights(pairs["v"], sigma_m)
        n = np.bincount(cell, weights=weight, minlength=len(centres))

        code = species_codes[record]
        listed = code >= 0
        cell_and_species = cell[listed] * n_species + code[listed]
        k = np.bincount(cell_and_species, weights=weight[listed], minlength=len(centres) * n_species)
        return n, k.reshape(len(centres), n_species)

    @staticmethod
    def beta_share(k: np.ndarray, n: np.ndarray, p0: float, prior_records: float) -> np.ndarray:
        """Share of the species around each cell, pulled toward p0 as if `prior_records` average records were added."""
        return (k + prior_records * p0) / (n + prior_records)

    @classmethod
    def share_range(
        cls,
        share: np.ndarray,
        n: np.ndarray,
        prior_records: float,
    ) -> tuple[np.ndarray, np.ndarray]:
        """Approximate 95% range of the share (normal approximation), clipped to [0, 1]."""
        half_width = cls.Z_95 * np.sqrt(share * (1 - share) / (n + prior_records))
        return np.maximum(0, share - half_width), np.minimum(1, share + half_width)

    @staticmethod
    def species_average(records: pd.DataFrame, species_key: int) -> float:
        """p0: the species' share of all records."""
        return float((records["speciesKey"] == species_key).mean())

    @classmethod
    def scale_max(
        cls,
        records: pd.DataFrame,
        species_key: int,
        sigma_m: float = 5_000,
        min_records: float = 20,
        quantile: float = 0.95,
    ) -> float:
        """vmax: the top of the colour scale, the 95th percentile of the smoothed share on a sigma-spaced lattice."""
        xy = records[["x", "y"]].to_numpy()
        is_species = (records["speciesKey"] == species_key).to_numpy(dtype=float)
        xs = np.arange(xy[:, 0].min(), xy[:, 0].max(), sigma_m)
        ys = np.arange(xy[:, 1].min(), xy[:, 1].max(), sigma_m)
        centres = np.array([(x, y) for x in xs for y in ys])

        shares = []
        nearby = cKDTree(xy).query_ball_point(centres, r=cls.reach_m(sigma_m))
        for centre, idx in zip(centres, nearby):
            if not idx:
                continue
            weight = cls.gaussian_weights(np.linalg.norm(xy[idx] - centre, axis=1), sigma_m)
            if weight.sum() >= min_records:
                shares.append(weight @ is_species[idx] / weight.sum())
        return float(np.quantile(shares, quantile))
