import { useEffect, useState } from 'react';
import { getSightingsRun } from '@/services/api';
import type { SightingsRun } from '@/types/api';

// Fetched once per page: the run (with its tile index) changes at most weekly.
let runPromise: Promise<SightingsRun> | null = null;

/** The latest sightings run, fetched the first time `enabled` is true (the heat-map layer is switched on). */
export function useSightingsRun(enabled: boolean): { run: SightingsRun | null; error: string | null } {
  const [run, setRun] = useState<SightingsRun | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled || run) return;
    let cancelled = false;
    runPromise ??= getSightingsRun();
    runPromise
      .then((r) => !cancelled && setRun(r))
      .catch(() => {
        runPromise = null;
        if (!cancelled) setError('No sightings run yet: run the sightings-refresh job.');
      });
    return () => {
      cancelled = true;
    };
  }, [enabled, run]);

  return { run, error };
}
