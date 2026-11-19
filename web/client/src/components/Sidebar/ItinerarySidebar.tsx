import { useMemo, useState } from 'react';
import type { Segment, PredictionMap, Trip } from '@/types/trip';
import type { NewSegmentInput } from '@/hooks/useTripData';
import { DayCard } from './DayCard';
import { AddRouteForm } from './AddRouteForm';

interface Props {
  trip: Trip | null;
  segments: Segment[];
  predictions: PredictionMap;
  selectedId: string | null;
  onSelect: (id: string) => void;
  loading: boolean;
  error: string | null;
  /** Saves the path; rejects when the API refuses it. */
  onAddSegment: (input: NewSegmentInput) => Promise<void>;
  onRemoveSegment: (id: string) => Promise<void>;
}

export function ItinerarySidebar({
  trip,
  segments,
  predictions,
  selectedId,
  onSelect,
  loading,
  error,
  onAddSegment,
  onRemoveSegment,
}: Props) {
  const [adding, setAdding] = useState(false);

  const grouped = useMemo(() => {
    const map = new Map<number, Segment[]>();
    segments.forEach((s) => {
      const arr = map.get(s.routeNumber) ?? [];
      arr.push(s);
      map.set(s.routeNumber, arr);
    });
    return Array.from(map.entries()).sort((a, b) => a[0] - b[0]);
  }, [segments]);

  const maxRounterNumber = segments.reduce((m, s) => Math.max(m, s.routeNumber), 0);
  const defaultDay = Math.max(1, maxRounterNumber + 1);

  return (
    <aside className="flex h-full w-[340px] shrink-0 flex-col border-r border-border bg-sidebar">
      <div className="border-b border-border px-5 py-4">
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary">
          Your trip
        </div>
        <h1 className="mt-1 text-base font-bold leading-tight text-sidebar-foreground">
          {trip?.title ?? 'Loading trip…'}
        </h1>
      </div>

      <div className="border-b border-border px-4 py-3">
        {adding ? (
          <AddRouteForm
            defaultDay={defaultDay}
            onAdd={async (input) => {
              await onAddSegment(input);
              setAdding(false);
            }}
            onCancel={() => setAdding(false)}
          />
        ) : (
          <button
            type="button"
            onClick={() => setAdding(true)}
            className="w-full rounded-xl border border-dashed border-border bg-card/50 py-2.5 text-sm font-semibold text-primary hover:border-primary/60 hover:bg-primary/5"
          >
            + Add new route
          </button>
        )}
      </div>

      <div className="flex-1 space-y-4 overflow-y-auto px-4 py-4">
        {loading && <p className="px-1 text-xs text-muted-foreground">Loading saved paths…</p>}
        {error && <p className="px-1 text-xs text-destructive">{error}</p>}
        {!loading && !error && segments.length === 0 && (
          <p className="px-1 text-xs leading-relaxed text-muted-foreground">
            No paths yet. Add your first drive above; it is saved on the server and comes back after a reload.
          </p>
        )}
        {grouped.map(([day, segs]) => (
          <section key={day} className="space-y-2">
            <div className="space-y-2">
              {segs.map((s) => (
                <DayCard
                  key={s.id}
                  segment={s}
                  prediction={predictions[s.id]}
                  selected={selectedId === s.id}
                  onSelect={onSelect}
                  onRemove={onRemoveSegment}
                />
              ))}
            </div>
          </section>
        ))}
      </div>
    </aside>
  );
}
