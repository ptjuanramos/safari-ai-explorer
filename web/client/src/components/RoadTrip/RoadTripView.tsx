import { useMemo } from 'react';
import { TripStats } from '@/components/BottomBar/TripStats';
import { MapPanel } from '@/components/Map/MapPanel';
import { ItinerarySidebar } from '@/components/Sidebar/ItinerarySidebar';
import { DatePicker } from '@/components/TopBar/DatePicker';
import { useDrivingRoutes } from '@/hooks/useDrivingRoutes';
import { useRoadRisk } from '@/hooks/useRoadRisk';
import { useRoadRiskPlaces } from '@/hooks/useRoadRiskPlaces';
import { useSelectedSegment } from '@/hooks/useSelectedSegment';
import { addDaysISO, type NewSegmentInput } from '@/hooks/useTripData';
import type { Segment, Trip } from '@/types/trip';

interface Props {
  trip: Trip;
  segments: Segment[];
  /** True while the saved paths load. */
  loading: boolean;
  /** Why the saved paths couldn't be loaded. */
  error: string | null;
  addSegment: (input: NewSegmentInput) => Promise<void>;
  removeSegment: (id: string) => Promise<void>;
  date: string;
  onDate: (date: string) => void;
}

/** The itinerary, the map, and the road risk of each day's drive. */
export function RoadTripView({
  trip,
  segments,
  loading: loadingPaths,
  error: pathsError,
  addSegment,
  removeSegment,
  date,
  onDate,
}: Props) {
  const { selectedId, select } = useSelectedSegment();
  const { paths, problem } = useDrivingRoutes(segments);

  // Each route follows its driving path once Google has returned one.
  const routed = useMemo(
    () =>
      segments.map((s) => ({
        ...s,
        path: paths[s.id] && paths[s.id].length > 1 ? paths[s.id] : s.path,
      })),
    [segments, paths, date],
  );

  const { predictions, report, loading, error } = useRoadRisk(date, routed);
  const places = useRoadRiskPlaces();
  // The road-risk data covers the mapped roads of the parks in jobs/settings.toml only; routes outside them come back
  // with coverage 0 and no score.
  const noRouteOnMappedRoads = report != null && report.routes.every((r) => r.meanSurfaceRisk == null);
  // The park-roads layer: each park's run in use for these routes, or its latest run before there is a report.
  const networkRuns = useMemo(
    () =>
      report
        ? report.runs.map((r) => ({ place: r.place, date: r.date }))
        : places.map((p) => ({ place: p.place, date: p.latestDate })),
    [report, places],
  );
  // The oldest rain day among the runs used, and whether any of those days is still partial.
  const oldestRainDay = report?.runs.map((r) => r.date).sort()[0];
  const anyPartial = report?.runs.some((r) => !r.complete) ?? false;

  return (
    <div className="flex min-h-0 flex-1">
      <ItinerarySidebar
        trip={{ ...trip, startDate: date }}
        segments={routed}
        predictions={predictions}
        selectedId={selectedId}
        onSelect={select}
        loading={loadingPaths}
        error={pathsError}
        onAddSegment={addSegment}
        onRemoveSegment={async (id) => {
          if (selectedId === id) select(null);
          await removeSegment(id);
        }}
      />
      <div className="flex flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between gap-4 border-b border-border bg-card px-5">
          <div className="min-w-0 text-xs text-muted-foreground">
            {error ? (
              <span className="text-condition-warn-foreground">{error}</span>
            ) : noRouteOnMappedRoads ? (
              <span className="text-condition-warn-foreground">
                None of these routes runs on the mapped park roads.
              </span>
            ) : report && oldestRainDay ? (
              <span title={report.runs.map((r) => `rain to ${r.date}`).join(', ')}>
                Road risk from rain up to <b className="text-foreground">{oldestRainDay}</b>
                {anyPartial && ' (some days still partial)'}
                {report.requestedDate > oldestRainDay && ' — current conditions, not a forecast'}. Mapped roads of{' '}
                {report.runs.length} {report.runs.length === 1 ? 'park' : 'parks'} only.
              </span>
            ) : (
              <span>Rule-based road risk from recent rain (unvalidated)</span>
            )}
            {problem === 'off' && (
              <span className="ml-2">
                Routes drawn as straight lines: the API has no GOOGLE_MAPS_API_KEY (restart it after setting one).
              </span>
            )}
            {problem === 'failed' && (
              <span className="ml-2">Some routes drawn as straight lines: directions failed (is the API running?).</span>
            )}
          </div>
          <DatePicker value={date} onChange={onDate} loading={loading} />
        </header>
        <div className="relative flex-1">
          <MapPanel
            segments={routed}
            predictions={predictions}
            selectedId={selectedId}
            onSelect={select}
            networkRuns={networkRuns}
          />
          <TripStats segments={routed} predictions={predictions} />
        </div>
      </div>
    </div>
  );
}
