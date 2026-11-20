import type { Segment, Prediction } from '@/types/trip';
import { basisText, formatRisk, riskBgClass } from '@/lib/roadRisk';

interface Props {
  segment: Segment;
  prediction?: Prediction;
  onClose: () => void;
}

export function SegmentDetailCard({ segment, prediction, onClose }: Props) {
  return (
    <div className="pointer-events-auto absolute inset-x-3 bottom-[4.5rem] z-20 max-h-[55%] overflow-y-auto rounded-2xl border border-border bg-card/95 p-4 shadow-2xl backdrop-blur sm:inset-x-auto sm:bottom-auto sm:right-4 sm:top-4 sm:max-h-[calc(100%-7rem)] sm:w-80">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
            Route {segment.routeNumber} · {segment.date}
          </div>
          <h3 className="mt-0.5 text-base font-bold text-foreground">
            {segment.origin} → {segment.destination}
          </h3>
        </div>
        <button
          onClick={onClose}
          aria-label="Close details"
          className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          ✕
        </button>
      </div>

      {prediction ? (
        <div className="mt-3 space-y-3">
          <div>
            <div className="flex items-center justify-between text-xs">
              <span className="font-medium text-muted-foreground">Surface risk</span>
              <span className="font-bold text-foreground">{formatRisk(prediction.surfaceRisk)}</span>
            </div>
            <div className="mt-1.5 h-2.5 w-full overflow-hidden rounded-full bg-muted">
              <div
                className={`h-full ${riskBgClass(prediction.surfaceRisk)}`}
                style={{ width: formatRisk(prediction.surfaceRisk) }}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <Stat label="Level" value={prediction.riskLevel} />
            <Stat label="Confidence" value={prediction.forecastConfidence} />
          </div>

          <div className="rounded-lg bg-muted/50 p-2.5">
            {prediction.avoidNightDriving && (
              <div className="mt-1 text-xs text-condition-warn-foreground">
                ⚠ Avoid night driving on this segment
              </div>
            )}
          </div>

          {prediction.basis && (
            <p className="border-t border-border pt-2 text-[11px] leading-relaxed text-muted-foreground">
              {basisText(prediction.basis)}
              {prediction.basis.maxSurfaceRisk != null &&
                ` Worst stretch: ${formatRisk(prediction.basis.maxSurfaceRisk)}.`}
            </p>
          )}
        </div>
      ) : (
        <p className="mt-3 text-xs leading-relaxed text-muted-foreground">
          No road-risk estimate for this route. The road risk covers the mapped roads of the northern parks
          (Serengeti, Ngorongoro, Tarangire, Lake Manyara, Arusha) only, and needs a run from the 7 days before the date.
        </p>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-border bg-background px-2 py-1.5">
      <div className="text-[10px] uppercase tracking-wider text-muted-foreground">{label}</div>
      <div className="text-xs font-semibold capitalize text-foreground">{value}</div>
    </div>
  );
}
