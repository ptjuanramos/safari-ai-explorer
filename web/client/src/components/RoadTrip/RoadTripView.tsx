import { useMemo, useState } from 'react';
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

/** Tailwind's `md` breakpoint: from here up the route list sits beside the map instead of over it. */
const WIDE = '(min-width: 768px)';

/** The itinerary, the map, and the road risk of each drive. */
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
  // The route list: beside the map on wide screens (open at first), a drawer over the map on phones (closed at first,
  // so the map gets the whole screen). The same button opens and closes it.
  const [routesOpen, setRoutesOpen] = useState(() => window.matchMedia(WIDE).matches);
  const isWide = () => window.matchMedia(WIDE).matches;

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
    <div className="relative flex min-h-0 flex-1">
      {routesOpen && (
        <button
          type="button"
          aria-label="Close the route list"
          onClick={() => setRoutesOpen(false)}
          className="fixed inset-0 z-30 bg-black/40 md:hidden"
        />
      )}
      <div
        id="route-list"
        className={`fixed inset-y-0 left-0 z-40 w-[min(340px,88vw)] shadow-2xl transition-transform duration-200 md:static md:z-auto md:w-auto md:shadow-none md:transition-none ${
          routesOpen ? 'translate-x-0' : '-translate-x-full md:hidden'
        }`}
      >
        <ItinerarySidebar
          trip={{ ...trip, startDate: date }}
          segments={routed}
          predictions={predictions}
          selectedId={selectedId}
          onSelect={(id) => {
            select(id);
            // On a phone, show the chosen route on the map instead of keeping the list over it.
            if (!isWide()) setRoutesOpen(false);
          }}
          loading={loadingPaths}
          error={pathsError}
          onAddSegment={addSegment}
          onRemoveSegment={async (id) => {
            if (selectedId === id) select(null);
            await removeSegment(id);
          }}
          onClose={() => setRoutesOpen(false)}
        />
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex shrink-0 flex-wrap items-center gap-x-4 gap-y-1.5 border-b border-border bg-card px-3 py-2 md:h-14 md:flex-nowrap md:px-5 md:py-0">
          <button
            type="button"
            onClick={() => setRoutesOpen((open) => !open)}
            aria-expanded={routesOpen}
            aria-controls="route-list"
            title={routesOpen ? 'Hide the route list' : 'Show the route list'}
            className="flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-border bg-background px-2.5 text-sm font-semibold text-foreground shadow-sm hover:bg-muted"
          >
            <span aria-hidden className="text-base leading-none">
              {routesOpen ? '‹' : '☰'}
            </span>
            Routes
            {segments.length > 0 && (
              <span className="rounded-full bg-primary px-1.5 text-[11px] leading-5 text-primary-foreground">
                {segments.length}
              </span>
            )}
          </button>
          <div className="order-last line-clamp-2 min-w-0 basis-full text-[11px] text-muted-foreground md:order-none md:line-clamp-none md:flex-1 md:basis-auto md:text-xs">
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
          <div className="ml-auto shrink-0">
            <DatePicker value={date} onChange={onDate} loading={loading} />
          </div>
        </header>
        <div className="relative min-h-0 flex-1">
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
