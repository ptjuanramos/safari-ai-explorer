export type SurfaceType = 'paved' | 'gravel' | 'track';
/** Bands of the surface risk: low ≤ 30%, medium ≤ 60%, high above (lib/roadRisk.ts). */
export type RiskLevel = 'low' | 'medium' | 'high';
export type VehicleRecommendation = '2wd_ok' | '4wd_required' | 'landcruiser_essential';
export type ForecastConfidence = 'high' | 'medium' | 'low';

export interface Prediction {
  /** Length-weighted surface risk along the route, 0 (dry) to 1 (worst). Rule-based and unvalidated. */
  surfaceRisk: number;
  riskLevel: RiskLevel;
  avoidNightDriving: boolean;
  forecastConfidence: ForecastConfidence;
  /** What the prediction is based on (the road-risk run and how much of the route it covers). */
  basis?: {
    /** Slugs of the parks whose roads the route runs along. */
    places: string[];
    /** The oldest rain day among those parks' runs. */
    rainDay: string;
    rainComplete: boolean;
    requestedDate: string;
    coverage: number;
    maxSurfaceRisk: number | null;
    maxRainIndexMm: number | null;
  };
}

export interface Coord {
  x: number;
  y: number;
}

export interface LatLng {
  lat: number;
  lng: number;
}

export interface Segment {
  id: string;
  routeNumber: number;
  date: string;
  origin: string;
  destination: string;
  distanceKm: number;
  estimatedDurationMin: number;
  surfaceType: SurfaceType;
  coords: Coord[];
  path: LatLng[];
  prediction?: Prediction;
}

export interface Trip {
  id: string;
  title: string;
  startDate: string;
  endDate: string;
}

export type PredictionMap = Record<string, Prediction>;
