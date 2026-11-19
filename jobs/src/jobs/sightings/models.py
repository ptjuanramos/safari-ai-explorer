from dataclasses import dataclass

import numpy as np

from jobs.shared.geo import Geo

RECORD_COLUMNS = (
    "gbifID",
    "decimalLongitude",
    "decimalLatitude",
    "coordinateUncertaintyInMeters",
    "year",
    "speciesKey",
    "species",
)


@dataclass(frozen=True)
class SightingsGridSettings:
    """
    How the share grids are built. One layer is written per kernel width.

    Attributes:
        sigmas_m: Gaussian kernel widths (sigma) in meters, one layer each. They match the map's zooms 9 to 14.
        cell_fraction: Cell size as a fraction of sigma (0.5 gives cells of sigma / 2).
        tile_cells: Tile width and height, in cells.
        uncertainty_filter_max_sigma_m: Layers with sigma up to this width use only records whose
            coordinate uncertainty is at most sigma. Wider layers use every record.
        prior_records: Weight of the beta prior, in records. It pulls cells with few records toward the
            species average, so a handful of records can't make a hotspot.
        min_records: Cells with fewer weighted records (of all species) than this are left empty (null).
        confidence_records: Number of records at which the UI shows half confidence:
            n / (n + confidence_records).
    """

    sigmas_m: tuple[float, ...] = (10_000, 5_000, 2_000, 1_000, 500, 250)
    cell_fraction: float = 0.5
    tile_cells: int = 256
    uncertainty_filter_max_sigma_m: float = 2_000
    prior_records: float = 10
    min_records: float = 5
    confidence_records: float = 10

    def cell_m(self, sigma_m: float) -> float:
        return sigma_m * self.cell_fraction

    def max_uncertainty_m(self, sigma_m: float) -> float | None:
        return sigma_m if sigma_m <= self.uncertainty_filter_max_sigma_m else None


@dataclass
class ShareTile:
    """
    One tile of one species in one layer. All arrays have shape (rows, columns), with row 0 in the south.

    Attributes:
        species: Species name, as in `SightingsConfig.species`.
        sigma_m: Kernel width of the layer, in meters.
        cell_m: Cell size, in meters.
        tx: Tile column, counted from the west.
        ty: Tile row, counted from the south.
        x0: UTM x of the centre of the tile's south-west cell (EPSG:32736, meters).
        y0: UTM y of the centre of the tile's south-west cell.
        p0: The species' average share in this layer's records. The beta prior pulls toward it.
        share: Effort-adjusted share per cell, NaN where there are fewer than `min_records`.
        n: Weighted records of all species around each cell.
        lo: Lower end of the approximate 95% range of `share`.
        hi: Upper end of the approximate 95% range of `share`.
    """

    species: str
    sigma_m: float
    cell_m: float
    tx: int
    ty: int
    x0: float
    y0: float
    p0: float
    share: np.ndarray
    n: np.ndarray
    lo: np.ndarray
    hi: np.ndarray

    @property
    def shape(self) -> tuple[int, int]:
        return self.share.shape

    def bounds_wgs84(self) -> tuple[float, float, float, float]:
        rows, cols = self.shape
        half = self.cell_m / 2
        return Geo.utm_to_wgs84_bounds(
            self.x0 - half,
            self.y0 - half,
            self.x0 + (cols - 1) * self.cell_m + half,
            self.y0 + (rows - 1) * self.cell_m + half,
        )
