import { useEffect, useState } from 'react';
import { getRoadRiskPlaces } from '@/services/api';
import type { RoadRiskPlace } from '@/types/api';

/** The parks with road-risk data and their newest rain day, fetched once. Empty until loaded or on error. */
export function useRoadRiskPlaces(): RoadRiskPlace[] {
  const [places, setPlaces] = useState<RoadRiskPlace[]>([]);

  useEffect(() => {
    let cancelled = false;
    getRoadRiskPlaces()
      .then((p) => !cancelled && setPlaces(p))
      .catch(() => {
        // Without the list, the park-roads layer waits for a route-risk report instead.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return places;
}
