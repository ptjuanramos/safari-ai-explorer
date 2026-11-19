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
    <div className="absolute bottom-0 left-0 right-0 z-10 border-t border-border bg-card/95 px-6 py-3 backdrop-blur">
      <div className="grid grid-cols-4 gap-6">
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
      <div className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
      <div
        className={`mt-0.5 text-lg font-bold ${
          tone === 'warn'
            ? 'text-condition-warn-foreground'
            : tone === 'good'
              ? 'text-condition-good-foreground'
              : 'text-foreground'
        }`}
      >
        {value}
      </div>
      {hint && <div className="text-[11px] text-muted-foreground">{hint}</div>}
    </div>
  );
}
