import type { SightingsLayer, SightingsTile, SpeciesSummary } from '@/types/api';

// How the heat map is drawn. The job only exports numbers (docs/data-contract.md); every visual choice is here.

/** Ground meters per screen pixel at a Web Mercator zoom and latitude. */
export const metersPerPixel = (zoom: number, lat: number) =>
  (156_543.033_92 * Math.cos((lat * Math.PI) / 180)) / 2 ** zoom;

/**
 * The layer whose kernel width is closest to max(250 m, 30 px on screen): the rule from the exploration notebook,
 * roughly z9 → 10 km, z10 → 5 km, z11 → 2 km, z12 → 1 km, z13 → 500 m, z14+ → 250 m.
 */
export function chooseLayer(layers: SightingsLayer[], zoom: number, lat: number): SightingsLayer | undefined {
  const target = Math.max(250, 30 * metersPerPixel(zoom, lat));
  return layers.reduce<SightingsLayer | undefined>(
    (best, l) => (!best || Math.abs(l.sigmaM - target) < Math.abs(best.sigmaM - target) ? l : best),
    undefined,
  );
}

export interface Bounds {
  west: number;
  south: number;
  east: number;
  north: number;
}

/** The layer's tiles that overlap the view. */
export const tilesInView = (layer: SightingsLayer, view: Bounds) =>
  layer.tiles.filter(
    ({ boundsWgs84: [w, s, e, n] }) => w < view.east && e > view.west && s < view.north && n > view.south,
  );

/** Below this many precise records of a species in a layer, the layer can't say much about it. */
export const FEW_RECORDS = 30;

// Yellow → orange → red → dark red (ColorBrewer YlOrRd), from the species average to the 95th percentile.
const RAMP: [number, number, number][] = [
  [255, 237, 160],
  [254, 178, 76],
  [253, 141, 60],
  [240, 59, 32],
  [189, 0, 38],
];

export const rampCss = `linear-gradient(to right, ${RAMP.map(([r, g, b]) => `rgb(${r},${g},${b})`).join(', ')})`;

/** The colour for a share, or null when it isn't above the species average (those cells stay clear). */
export function shareColour(share: number, scale: Pick<SpeciesSummary, 'p0' | 'vmax'>): [number, number, number] | null {
  if (share <= scale.p0 || scale.vmax <= scale.p0) return null;
  const t = Math.min(1, (share - scale.p0) / (scale.vmax - scale.p0)) * (RAMP.length - 1);
  const i = Math.min(RAMP.length - 2, Math.floor(t));
  const f = t - i;
  return RAMP[i].map((c, k) => Math.round(c + (RAMP[i + 1][k] - c) * f)) as [number, number, number];
}

/**
 * Draws a tile as a PNG, one pixel per cell. Colour fades with confidence n / (n + confidence_records), so areas
 * with few records of any species stay faint instead of looking like hotspots.
 */
export function renderTile(tile: SightingsTile, scale: Pick<SpeciesSummary, 'p0' | 'vmax'>): string {
  const canvas = document.createElement('canvas');
  canvas.width = tile.nx;
  canvas.height = tile.ny;
  const ctx = canvas.getContext('2d')!;
  const image = ctx.createImageData(tile.nx, tile.ny);
  for (let row = 0; row < tile.ny; row++) {
    for (let col = 0; col < tile.nx; col++) {
      const i = row * tile.nx + col;
      const share = tile.share[i];
      const colour = share == null ? null : shareColour(share, scale);
      if (!colour) continue;
      // Rows count from the south; canvas rows count from the top.
      const p = ((tile.ny - 1 - row) * tile.nx + col) * 4;
      const confidence = tile.n[i] / (tile.n[i] + tile.confidenceRecords);
      image.data.set([...colour, Math.round(255 * 0.85 * confidence)], p);
    }
  }
  ctx.putImageData(image, 0, 0);
  return canvas.toDataURL('image/png');
}

/** "10 km", "500 m". */
export const formatMeters = (m: number) => (m >= 1000 ? `${m / 1000} km` : `${m} m`);
