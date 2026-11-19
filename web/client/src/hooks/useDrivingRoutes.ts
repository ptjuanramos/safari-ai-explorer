import { useEffect, useState } from "react";
import type { LatLng, Segment } from "@/types/trip";
import { ApiError, getDrivingRoute } from "@/services/api";

export type RoutePaths = Record<string, LatLng[]>;

const cache = new Map<string, LatLng[]>();

/** Why some routes are still straight lines: `off` = the API has no GOOGLE_MAPS_API_KEY (503), `failed` = any other error. */
export type DirectionsProblem = 'off' | 'failed' | null;

function decodePolyline(encoded: string): LatLng[] {
  const path: LatLng[] = [];
  let index = 0;
  let lat = 0;
  let lng = 0;

  while (index < encoded.length) {
    let result = 0;
    let shift = 0;
    let byte = 0;
    do {
      byte = encoded.charCodeAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);
    lat += result & 1 ? ~(result >> 1) : result >> 1;

    result = 0;
    shift = 0;
    do {
      byte = encoded.charCodeAt(index++) - 63;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);
    lng += result & 1 ? ~(result >> 1) : result >> 1;

    path.push({ lat: lat / 1e5, lng: lng / 1e5 });
  }

  return path;
}

function keyOf(s: Segment): string {
  const a = s.path[0];
  const b = s.path[s.path.length - 1];
  if (!a || !b) return s.id;
  return `${a.lat.toFixed(5)},${a.lng.toFixed(5)}->${b.lat.toFixed(5)},${b.lng.toFixed(5)}`;
}

/**
 * Driving paths per segment id, plus why some are missing. Failures aren't remembered: every change of the segments
 * asks the API again, so a key added later (and an API restart) is picked up without a page reload.
 */
export function useDrivingRoutes(segments: Segment[]): { paths: RoutePaths; problem: DirectionsProblem } {
  const [paths, setPaths] = useState<RoutePaths>({});
  const [problem, setProblem] = useState<DirectionsProblem>(null);

  useEffect(() => {
    let cancelled = false;
    // One 503 is enough to know the key is missing; don't send the other segments.
    let directionsOff = false;

    async function run() {
      setProblem(null);
      await Promise.all(
        segments.map(async (seg) => {
          if (cancelled) return;
          const k = keyOf(seg);
          const cached = cache.get(k);
          if (cached) {
            setPaths((p) => (p[seg.id] === cached ? p : { ...p, [seg.id]: cached }));
            return;
          }
          const a = seg.path[0];
          const b = seg.path[seg.path.length - 1];
          if (!a || !b || directionsOff) return;
          try {
            const intermediates = seg.path
              .slice(1, -1)
              .map((p) => ({ lat: p.lat, lng: p.lng }));
            const res = await getDrivingRoute(a, b, intermediates);
            if (!res.encodedPolyline) {
              console.warn("[useDrivingRoutes] no road route for", seg.id);
              return;
            }
            const decoded = decodePolyline(res.encodedPolyline);
            cache.set(k, decoded);
            if (cancelled) return;
            setPaths((p) => ({ ...p, [seg.id]: decoded }));
          } catch (err) {
            if (cancelled) return;
            if (err instanceof ApiError && err.status === 503) {
              directionsOff = true;
              setProblem("off");
              return;
            }
            // 404 = Google found no road route between the points: that segment stays a straight line.
            if (!(err instanceof ApiError && err.status === 404)) {
              console.warn("[useDrivingRoutes] failed for", seg.id, err);
              setProblem((p) => p ?? "failed");
            }
          }
        }),
      );
    }

    run();
    return () => {
      cancelled = true;
    };
  }, [segments]);

  return { paths, problem };
}
