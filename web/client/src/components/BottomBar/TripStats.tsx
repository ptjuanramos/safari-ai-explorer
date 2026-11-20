import type { Segment, PredictionMap } from '@/types/trip';

interface Props {
  segments: Segment[];
  predictions: PredictionMap;
}

export function TripStats({ segments, predictions }: Props) {
  const totalKm = segments.reduce((s, x) => s + x.distanceKm, 0);
  const totalMin = segments.reduce((s, x) => s + x.estimatedDurationMin, 0);
  const flagged = segments.filter((s) => {
    const p = predictions[s.id];
    return p && p.riskLevel !== 'low';
  }).length;


  return (
    <div className="absolute bottom-0 left-0 right-0 z-10 border-t border-border bg-card/95 px-3 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur sm:px-6 sm:py-3">
      <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 sm:gap-6">
        <Stat label="Total distance" value={`${totalKm} km`} />
        <Stat
          label="Drive time"
          value={`${Math.floor(totalMin / 60)}h ${totalMin % 60}m`}
          hint="incl. +30% buffer"
        />
        <Stat
          label="Segments flagged"
          value={String(flagged)}
          hint={flagged ? 'medium or high surface risk' : 'all clear'}
          tone={flagged > 0 ? 'warn' : 'good'}
        />
      </div>
    </div>
  );
}

function Stat({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: 'good' | 'warn';
}) {
  return (
    <div>
      <div className="truncate text-[9px] font-semibold uppercase tracking-wider text-muted-foreground sm:text-[10px]">
        {label}
      </div>
      <div
        className={`mt-0.5 text-base font-bold sm:text-lg ${
          tone === 'warn'
            ? 'text-condition-warn-foreground'
            : tone === 'good'
              ? 'text-condition-good-foreground'
              : 'text-foreground'
        }`}
      >
        {value}
      </div>
      {hint && <div className="hidden text-[11px] text-muted-foreground sm:block">{hint}</div>}
    </div>
  );
}
