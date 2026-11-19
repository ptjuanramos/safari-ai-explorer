import { FEW_RECORDS, formatMeters, rampCss } from '@/lib/sightings';
import type { SightingsLayer, SightingsRun } from '@/types/api';

interface Props {
  run: SightingsRun | null;
  error: string | null;
  species: string | null;
  onSpecies: (name: string) => void;
  layer: SightingsLayer | undefined;
  loadingTiles: number;
}

const pct = (x: number) => `${(x * 100).toFixed(x < 0.1 ? 1 : 0)}%`;

/** The heat-map layer's controls on the map: species, colour scale, and what the current zoom can show. */
export function SightingsPanel({ run, error, species, onSpecies, layer, loadingTiles }: Props) {
  const scale = run?.species.find((s) => s.name === species);
  const precise = species ? (layer?.speciesRecords[species] ?? 0) : 0;

  return (
    <div className="pointer-events-auto absolute left-4 top-4 z-10 max-h-[calc(100%-12rem)] w-72 overflow-y-auto rounded-xl border border-border bg-card/95 p-3 text-xs shadow-lg backdrop-blur">
      <div className="flex items-center justify-between gap-2">
        <div className="font-semibold text-foreground">Where visitors see wildlife</div>
        {loadingTiles > 0 && <span className="text-[10px] text-muted-foreground">Loading {loadingTiles}…</span>}
      </div>

      {error && <p className="mt-2 text-destructive">{error}</p>}
      {!run && !error && <p className="mt-2 text-muted-foreground">Loading sightings…</p>}

      {run && (
        <>
          <div className="mt-2 flex flex-wrap gap-1">
            {run.species.map((s) => (
              <button
                key={s.name}
                type="button"
                onClick={() => onSpecies(s.name)}
                aria-pressed={s.name === species}
                title={`${s.records.toLocaleString()} records`}
                className={`rounded-full border px-2 py-0.5 capitalize transition-colors ${
                  s.name === species
                    ? 'border-primary bg-primary/10 font-semibold text-foreground'
                    : 'border-border bg-card text-muted-foreground hover:border-primary/40'
                }`}
              >
                {s.name}
              </button>
            ))}
          </div>

          {scale && (
            <div className="mt-3">
              <div className="mb-1 text-[11px] text-foreground">Share of mammal records that were {species}</div>
              <div className="h-2 w-full rounded-full" style={{ background: rampCss }} />
              <div className="mt-0.5 flex justify-between text-[10px] text-muted-foreground">
                <span>{pct(scale.p0)} (average)</span>
                <span>{pct(scale.vmax)}+</span>
              </div>
            </div>
          )}

          {layer && species && (
            <p className="mt-2 text-[10px] leading-relaxed text-muted-foreground">
              At this zoom: smoothed over {formatMeters(layer.sigmaM)}, from {precise.toLocaleString()} {species}{' '}
              records{layer.maxUncertaintyM != null && ` located to within ${formatMeters(layer.maxUncertaintyM)}`}.
            </p>
          )}
          {layer && species && precise < FEW_RECORDS && (
            <p className="mt-1.5 rounded-md bg-condition-warn/15 p-2 text-[10px] leading-relaxed text-foreground">
              Too few precise {species} records to map at this zoom. Most sightings come from iNaturalist with the
              location hidden (moved up to about 30 km). Zoom out for the 5 or 10 km view.
            </p>
          )}

          <p className="mt-2 border-t border-border pt-2 text-[10px] leading-relaxed text-muted-foreground">
            GBIF records {run.startYear}–{run.endYear}, updated {run.date}.
          </p>
        </>
      )}
    </div>
  );
}
