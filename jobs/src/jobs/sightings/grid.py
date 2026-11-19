from collections.abc import Iterator
from dataclasses import dataclass

import numpy as np
import pandas as pd

from jobs.shared.geo import Geo
from jobs.sightings.kernel import KernelShare
from jobs.sightings.models import ShareTile, SightingsGridSettings
from jobs.sightings.records import LayerRecords


@dataclass(frozen=True)
class TileGrid:
    """
    The whole-box UTM grid of one layer, cut into square tiles.

    Attributes:
        xs: Cell-centre x of each column, in UTM meters, west to east.
        ys: Cell-centre y of each row, in UTM meters, south to north.
        tile_cells: Tile width and height, in cells.
    """

    xs: np.ndarray
    ys: np.ndarray
    tile_cells: int

    @classmethod
    def covering(cls, bounds: tuple[float, float, float, float], cell_m: float, tile_cells: int) -> "TileGrid":
        """The grid whose cells cover the lon/lat box, aligned to multiples of `cell_m`."""
        minx, miny, maxx, maxy = Geo.wgs84_to_utm_bounds(bounds)
        return cls(cls.cell_centres(minx, maxx, cell_m), cls.cell_centres(miny, maxy, cell_m), tile_cells)

    @staticmethod
    def cell_centres(low_m: float, high_m: float, cell_m: float) -> np.ndarray:
        first = np.floor(low_m / cell_m) * cell_m + cell_m / 2
        return np.arange(first, high_m + cell_m / 2, cell_m)

    @staticmethod
    def tile_count(n_cells: int, tile_cells: int) -> int:
        return -(-n_cells // tile_cells)

    def tiles(self) -> Iterator[tuple[int, int, np.ndarray, np.ndarray]]:
        """Yields (tx, ty, xs, ys) for each tile, from the south-west, row by row."""
        size = self.tile_cells
        for ty in range(self.tile_count(len(self.ys), size)):
            for tx in range(self.tile_count(len(self.xs), size)):
                yield tx, ty, self.xs[tx * size : (tx + 1) * size], self.ys[ty * size : (ty + 1) * size]


@dataclass(frozen=True)
class ShareTileBuilder:
    """
    Builds the share tiles of one layer for every species.

    Attributes:
        species: Species on the map, name -> GBIF speciesKey.
        bounds: The box, as (west, south, east, north) in degrees.
        settings: Grid settings (kernel widths, prior, tile size...).
    """

    species: dict[str, int]
    bounds: tuple[float, float, float, float]
    settings: SightingsGridSettings

    def build(self, records: pd.DataFrame, sigma_m: float) -> Iterator[ShareTile]:
        """Yields one tile at a time, skipping tiles with no records nearby or where every cell is too thin."""
        layer = LayerRecords.for_layer(records, sigma_m, self.settings)
        xy = layer[["x", "y"]].to_numpy()
        codes = self._species_codes(layer)
        averages = {name: KernelShare.species_average(layer, key) for name, key in self.species.items()}
        reach_m = KernelShare.reach_m(sigma_m)
        grid = TileGrid.covering(self.bounds, self.settings.cell_m(sigma_m), self.settings.tile_cells)

        for tx, ty, xs, ys in grid.tiles():
            near = self._records_near(xy, xs, ys, reach_m)
            if not near.any():
                continue

            n, k = KernelShare.weighted_counts(xy[near], codes[near], len(self.species), self._centres(xs, ys), sigma_m)
            empty = n < self.settings.min_records
            if empty.all():
                continue

            for i, name in enumerate(self.species):
                yield self._tile(name, sigma_m, tx, ty, xs, ys, averages[name], k[:, i], n, empty)

    def _species_codes(self, records: pd.DataFrame) -> np.ndarray:
        """Per record, the index of its species in `self.species`, or -1 for any other species."""
        index = {key: i for i, key in enumerate(self.species.values())}
        return records["speciesKey"].map(index).fillna(-1).astype(int).to_numpy()

    @staticmethod
    def _records_near(xy: np.ndarray, xs: np.ndarray, ys: np.ndarray, reach_m: float) -> np.ndarray:
        """Records within reach of the tile; the others weigh almost nothing in it."""
        x, y = xy[:, 0], xy[:, 1]
        return (x >= xs[0] - reach_m) & (x <= xs[-1] + reach_m) & (y >= ys[0] - reach_m) & (y <= ys[-1] + reach_m)

    @staticmethod
    def _centres(xs: np.ndarray, ys: np.ndarray) -> np.ndarray:
        gx, gy = np.meshgrid(xs, ys)
        return np.column_stack([gx.ravel(), gy.ravel()])

    def _tile(
        self,
        name: str,
        sigma_m: float,
        tx: int,
        ty: int,
        xs: np.ndarray,
        ys: np.ndarray,
        p0: float,
        k: np.ndarray,
        n: np.ndarray,
        empty: np.ndarray,
    ) -> ShareTile:
        prior = self.settings.prior_records
        share = KernelShare.beta_share(k, n, p0, prior)
        lo, hi = KernelShare.share_range(share, n, prior)
        shape = (len(ys), len(xs))

        def masked(values: np.ndarray) -> np.ndarray:
            return np.where(empty, np.nan, values).reshape(shape)

        return ShareTile(
            species=name,
            sigma_m=sigma_m,
            cell_m=self.settings.cell_m(sigma_m),
            tx=tx,
            ty=ty,
            x0=float(xs[0]),
            y0=float(ys[0]),
            p0=p0,
            share=masked(share),
            n=n.reshape(shape),
            lo=masked(lo),
            hi=masked(hi),
        )
