import { API_BASE_URL } from '@/config';
import type {
  DrivingRoute,
  NewUserPath,
  RoadNetwork,
  RoadRiskDates,
  RoadRiskPlace,
  RouteRiskReport,
  SightingsRun,
  SightingsTile,
  UserPath,
} from '@/types/api';
import type { LatLng } from '@/types/trip';

/** An API error with its HTTP status, so callers can tell "no data" (404) from "not configured" (503). */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: { Accept: 'application/json', ...(init?.body ? { 'Content-Type': 'application/json' } : {}) },
  });
  if (!res.ok) throw new ApiError(res.status, `${init?.method ?? 'GET'} ${path} failed (${res.status})`);
  // 204 No Content (a DELETE) has no body to parse.
  return (res.status === 204 ? undefined : res.json()) as Promise<T>;
}

const post = <T>(path: string, body: unknown, signal?: AbortSignal) =>
  request<T>(path, { method: 'POST', body: JSON.stringify(body), signal });

// Road risk (rule-based, from IMERG rain over OSM roads; one run per park, see jobs/settings.toml).

export const getRoadRiskPlaces = () => request<RoadRiskPlace[]>('/api/road-risk/places');

export const getRoadRiskDates = () => request<RoadRiskDates>('/api/road-risk/dates');

export const getRoadNetwork = (date: string, place: string) =>
  request<RoadNetwork>(`/api/road-risk/${date}?place=${encodeURIComponent(place)}`);

/** Scores the routes against every park with data (no ?place). */
export const getRouteRisk = (date: string, routes: { id: string; path: LatLng[] }[], signal?: AbortSignal) =>
  post<RouteRiskReport>('/api/road-risk/routes', { date, routes }, signal);

// Driving directions (Google Routes through the API, which holds the server key).

export const getDrivingRoute = (origin: LatLng, destination: LatLng, intermediates: LatLng[]) =>
  post<DrivingRoute>('/api/directions', { origin, destination, intermediates });

// User paths (the trip's drives, saved by the API).

export const listUserPaths = () => request<UserPath[]>('/api/user-paths');

export const addUserPath = (path: NewUserPath) => post<UserPath>('/api/user-paths', path);

export const deleteUserPath = (id: string) =>
  request<void>(`/api/user-paths/${encodeURIComponent(id)}`, { method: 'DELETE' });

// Sightings (GBIF, effort-adjusted share tiles).

export const getSightingsRun = () => request<SightingsRun>('/api/sightings/latest');

export const getSightingsTile = (species: string, sigmaM: number, tx: number, ty: number) =>
  request<SightingsTile>(`/api/sightings/${species}/${sigmaM}/${tx}/${ty}`);
