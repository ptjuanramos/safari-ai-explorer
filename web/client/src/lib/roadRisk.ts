import type { RoadRiskRun, RouteRisk } from '@/types/api';
import type { Prediction, RiskLevel, SurfaceType } from '@/types/trip';

export function riskLevel(risk: number): RiskLevel {
  if (risk <= 0.3) return 'low';
  if (risk <= 0.6) return 'medium';
  return 'high';
}

const LEVEL_HEX: Record<RiskLevel, string> = { low: '#16a34a', medium: '#f59e0b', high: '#dc2626' };
const LEVEL_BG: Record<RiskLevel, string> = {
  low: 'bg-condition-good',
  medium: 'bg-condition-warn',
  high: 'bg-condition-bad',
};

export const riskHex = (risk: number) => LEVEL_HEX[riskLevel(risk)];

export const riskBgClass = (risk: number) => LEVEL_BG[riskLevel(risk)];

export const formatRisk = (risk: number) => `${Math.round(risk * 100)}%`;

const daysBetween = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000);

/** The runs of the parks the route touches (all runs if it touches none, which toPrediction never scores). */
const runsFor = (risk: RouteRisk, runs: RoadRiskRun[]) => {
  const used = runs.filter((r) => risk.places.includes(r.place));
  return used.length ? used : runs;
};

/**
 * How much to trust the estimate: high only when the rain days are complete, close to the trip date, and most of the
 * route runs on mapped park roads. A rain day long before the trip (or a future trip) says less about that day.
 */
function confidenceFor(risk: RouteRisk, rainDay: string, complete: boolean, requestedDate: string) {
  const gap = daysBetween(rainDay, requestedDate);
  if (risk.coverage < 0.3 || gap > 7) return 'low';
  if (complete && risk.coverage >= 0.8 && gap <= 1) return 'high';
  return 'medium';
}

export function toPrediction(
  risk: RouteRisk,
  runs: RoadRiskRun[],
  requestedDate: string,
  surface: SurfaceType,
): Prediction | undefined {
  if (risk.meanSurfaceRisk == null) return undefined;
  const used = runsFor(risk, runs);
  const rainDay = used.map((r) => r.date).sort()[0];
  const complete = used.every((r) => r.complete);
  const level = riskLevel(risk.meanSurfaceRisk);
  return {
    surfaceRisk: risk.meanSurfaceRisk,
    riskLevel: level,
    avoidNightDriving: risk.meanSurfaceRisk > 0.4 || surface !== 'paved',
    forecastConfidence: confidenceFor(risk, rainDay, complete, requestedDate),
    basis: {
      places: risk.places,
      rainDay,
      rainComplete: complete,
      requestedDate,
      coverage: risk.coverage,
      maxSurfaceRisk: risk.maxSurfaceRisk,
      maxRainIndexMm: risk.maxRainIndexMm,
    },
  };
}

export function basisText(basis: NonNullable<Prediction['basis']>): string {
  const rain = `rain up to ${basis.rainDay}${basis.rainComplete ? '' : ' (still partial)'}`;
  const ahead = daysBetween(basis.rainDay, basis.requestedDate) > 1 ? ' — current conditions, not a forecast' : '';
  return `Rule-based estimate from ${rain}${ahead}. ${Math.round(basis.coverage * 100)}% of this route is on mapped park roads.`;
}
