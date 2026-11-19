import { useCallback, useEffect, useState } from 'react';
import { addUserPath, deleteUserPath, listUserPaths } from '@/services/api';
import type { UserPath } from '@/types/api';
import type { LatLng, Segment, SurfaceType, Trip } from '@/types/trip';

export interface NewSegmentInput {
  origin: string;
  destination: string;
  originLat?: number;
  originLng?: number;
  destinationLat: number;
  destinationLng: number;
  distanceKm: number;
  estimatedDurationMin: number;
  surfaceType: SurfaceType;
  routeNumber?: number;
}

export function addDaysISO(iso: string, days: number): string {
  const d = new Date(iso);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

const TRIP: Omit<Trip, 'durationDays' | 'startDate' | 'endDate'> = {
  id: 'my-trip',
  title: 'Northern Tanzania circuit'
};

/** A saved path as the UI's segment. `date` is filled in by RoadTripView from the departure date. */
const toSegment = (p: UserPath): Segment => ({
  id: p.id,
  routeNumber: p.routeNumber,
  date: '',
  origin: p.origin,
  destination: p.destination,
  distanceKm: p.distanceKm,
  estimatedDurationMin: p.durationMin,
  surfaceType: p.surfaceType,
  coords: [],
  path: p.stops,
});


/** The trip's paths, loaded from and saved to the API (DATA_DIR/user-paths/). */
export function useTripData() {
  const [segments, setSegments] = useState<Segment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listUserPaths()
      .then((paths) => setSegments(paths.map(toSegment)))
      .catch(() => setError('Saved paths could not be loaded (is the API running?).'))
      .finally(() => setLoading(false));
  }, []);

  /** Saves the path, then shows it. Throws when the API refuses it, so the form can say so. */
  const addSegment = useCallback(
    async (input: NewSegmentInput) => {
      const last = segments[segments.length - 1];
      const end: LatLng = { lat: input.destinationLat, lng: input.destinationLng };
      // Without an origin, the drive starts where the last one ended.
      const start: LatLng =
        input.originLat != null && input.originLng != null
          ? { lat: input.originLat, lng: input.originLng }
          : (last?.path[last.path.length - 1] ?? end);
      const saved = await addUserPath({
        routeNumber: input.routeNumber ?? (last?.routeNumber ?? 0) + 1,
        origin: input.origin,
        destination: input.destination,
        // Only the two ends: inner points are sent to Google as stops, and a straight-line midpoint could be off-road.
        stops: [start, end],
        surfaceType: input.surfaceType,
        distanceKm: input.distanceKm,
        durationMin: input.estimatedDurationMin,
      });
      setSegments((prev) => [...prev, toSegment(saved)]);
    },
    [segments],
  );

  const removeSegment = useCallback(async (id: string) => {
    await deleteUserPath(id);
    setSegments((prev) => prev.filter((s) => s.id !== id));
  }, []);

  const trip: Trip = {
    ...TRIP,
    startDate: '',
    endDate: ''
  };

  return { trip, segments, loading, error, addSegment, removeSegment };
}
