// Shapes returned by the .NET API (camelCase JSON). See docs/data-contract.md for what the numbers mean.

export interface RoadRiskRun {
  place: string;
  /** The rain day: the last day of the rain window. */
  date: string;
  provider: string;
  lookbackDays: number;
  edgeCount: number;
  /** False when some window day has fewer than 48 IMERG half-hours, so its rain is undercounted. */
  complete: boolean;
  granulesPerDay: number | null;
  granuleCoverage: Record<string, number>;
  note: string;
  writtenAt: string;
}

export interface RoadRiskDates {
  place: string;
  dates: string[];
}

export interface RouteRisk {
  id: string;
  routeLengthM: number;
  matchedLengthM: number;
  /** Share of the route along mapped park roads, 0 to 1. */
  coverage: number;
  matchedEdges: number;
  /** Length-weighted surface risk of the roads along the route, 0 to 1; null outside the mapped roads. */
  meanSurfaceRisk: number | null;
  maxSurfaceRisk: number | null;
  maxRainIndexMm: number | null;
  roadTypes: string[];
  /** Slugs of the parks whose roads the route runs along (see RouteRiskReport.runs). */
  places: string[];
}

/** One run per park with data recent enough for the date; parks can be on different rain days. */
export interface RouteRiskReport {
  runs: RoadRiskRun[];
  requestedDate: string;
  routes: RouteRisk[];
}

/** A park with road-risk data, from GET /api/road-risk/places. */
export interface RoadRiskPlace {
  place: string;
  latestDate: string;
}

export interface RoadNetwork {
  type: 'FeatureCollection';
  run: RoadRiskRun;
  features: {
    type: 'Feature';
    id: string;
    geometry: { type: 'LineString'; coordinates: [number, number][] };
    properties: {
      roadType: string;
      lengthM: number;
      travelTimeMin: number;
      rainIndexMm: number | null;
      surfaceRisk: number;
    };
  }[];
}

export interface DrivingRoute {
  encodedPolyline: string;
  distanceKm: number;
  durationMin: number;
}

export interface SpeciesSummary {
  name: string;
  speciesKey: number;
  records: number;
  /** Colour scale start: the species average share (5 km layer). */
  p0: number;
  /** Colour scale end: the 95th percentile of the 5 km share. */
  vmax: number;
}

export interface TileRef {
  tx: number;
  ty: number;
  /** west, south, east, north */
  boundsWgs84: [number, number, number, number];
}

export interface SightingsLayer {
  sigmaM: number;
  cellM: number;
  tileCells: number;
  /** Only records at least this precise are used; null = every record. */
  maxUncertaintyM: number | null;
  records: number;
  speciesRecords: Record<string, number>;
  speciesP0: Record<string, number>;
  grid: { x0: number; y0: number; nx: number; ny: number };
  tiles: TileRef[];
}

export interface SightingsRun {
  date: string;
  startYear: number;
  endYear: number;
  records: number;
  layers: SightingsLayer[];
  species: SpeciesSummary[];
  meaning: string;
  writtenAt: string;
}

export interface SightingsTile {
  species: string;
  crs: string;
  sigmaM: number;
  cellM: number;
  tx: number;
  ty: number;
  x0: number;
  y0: number;
  nx: number;
  ny: number;
  boundsWgs84: [number, number, number, number];
  p0: number;
  confidenceRecords: number;
  /** Row-major from the south: cell (row, col) is at row * nx + col. */
  share: (number | null)[];
  n: number[];
  lo: (number | null)[];
  hi: (number | null)[];
}

// User paths (saved by the API in DATA_DIR/user-paths/, ADR-0003).

/** A path as it is sent to POST /api/user-paths. `stops`: origin first, destination last. */
export interface NewUserPath {
  routeNumber: number;
  origin: string;
  destination: string;
  stops: { lat: number; lng: number }[];
  surfaceType: 'paved' | 'gravel' | 'track';
  distanceKm: number;
  durationMin: number;
}

export interface UserPath extends NewUserPath {
  id: string;
  createdAt: string;
}
