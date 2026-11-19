import { useState } from 'react';
import type { Segment, Prediction } from '@/types/trip';
import { formatRisk, riskBgClass } from '@/lib/roadRisk';

interface Props {
  segment: Segment;
  prediction?: Prediction;
  selected: boolean;
  onSelect: (id: string) => void;
  /** Deletes the saved path; rejects when the API fails. */
  onRemove: (id: string) => Promise<void>;
}

export function DayCard({ segment, prediction, selected, onSelect, onRemove }: Props) {
  const [removing, setRemoving] = useState(false);
  const [removeFailed, setRemoveFailed] = useState(false);

  const remove = async () => {
    if (!window.confirm(`Delete ${segment.origin} → ${segment.destination}?`)) return;
    setRemoving(true);
    setRemoveFailed(false);
    try {
      await onRemove(segment.id);
    } catch {
      setRemoveFailed(true);
      setRemoving(false);
    }
  };

  return (
    // A div, not a button: the card holds its own delete button, and buttons can't nest.
    <div
      role="button"
      tabIndex={0}
      onClick={() => onSelect(segment.id)}
      onKeyDown={(e) => {
        if (e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) {
          e.preventDefault();
          onSelect(segment.id);
        }
      }}
      className={`w-full cursor-pointer text-left rounded-xl border p-4 transition-all ${
        selected
          ? 'border-primary bg-primary/5 shadow-md'
          : 'border-border bg-card hover:border-primary/40 hover:bg-accent/40'
      } ${removing ? 'opacity-50' : ''}`}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Route {segment.routeNumber}
        </span>
        <span className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">{segment.date}</span>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              void remove();
            }}
            disabled={removing}
            title="Delete this path"
            aria-label={`Delete ${segment.origin} to ${segment.destination}`}
            className="rounded px-1 text-sm leading-none text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
          >
            ×
          </button>
        </span>
      </div>
      {removeFailed && <p className="mt-1 text-[11px] text-destructive">Could not delete it. Try again.</p>}
      <div className="mt-2 text-sm font-semibold text-foreground">
        {segment.origin} <span className="text-muted-foreground">→</span> {segment.destination}
      </div>

      <div className="mt-3 flex flex-wrap gap-1.5">
        <Pill>{segment.distanceKm} km</Pill>
        <Pill>{Math.floor(segment.estimatedDurationMin / 60)}h {segment.estimatedDurationMin % 60}m</Pill>
        <Pill variant="surface">{segment.surfaceType}</Pill>
      </div>

      {prediction && (
        <div className="mt-3 rounded-lg bg-muted/40 p-3">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs font-medium text-muted-foreground">
              Surface risk {formatRisk(prediction.surfaceRisk)}
            </span>
            <span
              className={`rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-white ${riskBgClass(prediction.surfaceRisk)}`}
            >
              {prediction.riskLevel}
            </span>
          </div>
          <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-border">
            <div
              className={`h-full ${riskBgClass(prediction.surfaceRisk)}`}
              style={{ width: formatRisk(prediction.surfaceRisk) }}
            />
          </div>
          {prediction.basis && (
            <p className="mt-1 text-[10px] text-muted-foreground">
              rain to {prediction.basis.rainDay} ·{' '}
              {Math.round(prediction.basis.coverage * 100)}% on park roads · {prediction.forecastConfidence} confidence
            </p>
          )}
        </div>
      )}
      {!prediction && (
        <p className="mt-3 text-[11px] text-muted-foreground">No road-risk estimate (outside the mapped park roads).</p>
      )}
    </div>
  );
}

function Pill({
  children,
  variant = 'default',
}: {
  children: React.ReactNode;
  variant?: 'default' | 'surface';
}) {
  return (
    <span
      className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${
        variant === 'surface'
          ? 'bg-secondary text-secondary-foreground'
          : 'bg-muted text-muted-foreground'
      }`}
    >
      {children}
    </span>
  );
}
