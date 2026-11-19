import { useEffect, useState } from 'react';
import { toPrediction } from '@/lib/roadRisk';
import { ApiError, getRouteRisk } from '@/services/api';
import type { RouteRiskReport } from '@/types/api';
import type { PredictionMap, Segment } from '@/types/trip';

export interface RoadRiskState {
  predictions: PredictionMap;
  report: RouteRiskReport | null;
  loading: boolean;
  /** Why there is no report: no run near the date (404), or the API failed. */
  error: string | null;
}

/** Road-risk predictions for every segment on a date, from POST /api/road-risk/routes. */
export function useRoadRisk(date: string, segments: Segment[]): RoadRiskState {
  const [state, setState] = useState<RoadRiskState>({ predictions: {}, report: null, loading: false, error: null });
  // Refetch only when a segment's route actually changes, not on every render.
  const key = segments.map((s) => `${s.id}:${s.surfaceType}:${s.path.length}`).join('|');

  useEffect(() => {
    if (!date || segments.length === 0) {
      // The last path was deleted (or none is saved yet): drop the old report.
      setState({ predictions: {}, report: null, loading: false, error: null });
      return;
    }
    const controller = new AbortController();
    setState((s) => ({ ...s, loading: true }));

    getRouteRisk(
      date,
      segments.map((s) => ({ id: s.id, path: s.path })),
      controller.signal,
    )
      .then((report) => {
        const predictions: PredictionMap = {};
        for (const risk of report.routes) {
          const segment = segments.find((s) => s.id === risk.id);
          const prediction = segment && toPrediction(risk, report.runs, report.requestedDate, segment.surfaceType);
          if (prediction) predictions[risk.id] = prediction;
        }
        setState({ predictions, report, loading: false, error: null });
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const error =
          err instanceof ApiError && err.status === 404
            ? `No road-risk run in the 7 days before ${date}.`
            : 'Road risk could not be loaded.';
        setState({ predictions: {}, report: null, loading: false, error });
      });

    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date, key]);

  return state;
}
