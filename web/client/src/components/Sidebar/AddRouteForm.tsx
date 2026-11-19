import { useState, type FormEvent } from 'react';
import type { NewSegmentInput } from '@/hooks/useTripData';
import { ApiError } from '@/services/api';
import type { SurfaceType } from '@/types/trip';
import { PlaceAutocompleteField } from './PlaceAutocompleteField';

interface Props {
  defaultDay: number;
  /** Saves the path; rejects when the API refuses it, and the form then stays open with the error. */
  onAdd: (input: NewSegmentInput) => Promise<void>;
  onCancel: () => void;
}

export function AddRouteForm({ defaultDay, onAdd, onCancel }: Props) {
  const [dayNumber, setDayNumber] = useState(String(defaultDay));
  const [origin, setOrigin] = useState('');
  const [destination, setDestination] = useState('');
  const [originLat, setOriginLat] = useState('');
  const [originLng, setOriginLng] = useState('');
  const [destLat, setDestLat] = useState('');
  const [destLng, setDestLng] = useState('');
  const [distanceKm, setDistanceKm] = useState('100');
  const [durationMin, setDurationMin] = useState('180');
  const [surfaceType, setSurfaceType] = useState<SurfaceType>('gravel');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const dLat = parseFloat(destLat);
    const dLng = parseFloat(destLng);
    if (!origin || !destination || Number.isNaN(dLat) || Number.isNaN(dLng)) return;
    const oLat = parseFloat(originLat);
    const oLng = parseFloat(originLng);
    setSaving(true);
    setSaveError(null);
    try {
      await onAdd({
      origin,
      destination,
      originLat: Number.isFinite(oLat) ? oLat : undefined,
      originLng: Number.isFinite(oLng) ? oLng : undefined,
      destinationLat: dLat,
      destinationLng: dLng,
      distanceKm: parseFloat(distanceKm) || 0,
      estimatedDurationMin: parseInt(durationMin, 10) || 0,
      surfaceType,
      dayNumber: Math.max(1, parseInt(dayNumber, 10) || defaultDay),
      });
    } catch (err) {
      setSaveError(
        err instanceof ApiError && err.status === 409
          ? 'Too many saved paths: delete some first.'
          : err instanceof ApiError && err.status === 400
            ? 'The API refused this path: check the day, places and numbers.'
            : 'Could not save the path (is the API running?).',
      );
      setSaving(false);
    }
  };

  return (
    <form
      onSubmit={submit}
      className="space-y-2 rounded-xl border border-primary/30 bg-card p-3 shadow-sm"
    >
      <div className="flex items-center justify-between">
        <div className="text-xs font-bold uppercase tracking-wider text-primary">
          New route
        </div>
        <label className="flex items-center gap-1.5 text-[10px] uppercase tracking-wider text-muted-foreground">
          Day
          <input
            className="input w-14"
            type="number"
            min={1}
            value={dayNumber}
            onChange={(e) => setDayNumber(e.target.value)}
          />
        </label>
      </div>

      <Field label="Origin">
        <PlaceAutocompleteField
          value={origin}
          onChange={setOrigin}
          onResolved={(loc) => {
            setOrigin(loc.name);
            setOriginLat(String(loc.lat));
            setOriginLng(String(loc.lng));
          }}
          placeholder="Search a place…"
        />
      </Field>
      <div className="grid grid-cols-2 gap-2">
        <Field label="Origin lat (optional)">
          <input
            className="input"
            value={originLat}
            onChange={(e) => setOriginLat(e.target.value)}
            placeholder="auto"
          />
        </Field>
        <Field label="Origin lng (optional)">
          <input
            className="input"
            value={originLng}
            onChange={(e) => setOriginLng(e.target.value)}
            placeholder="auto"
          />
        </Field>
      </div>

      <Field label="Destination">
        <PlaceAutocompleteField
          value={destination}
          onChange={setDestination}
          onResolved={(loc) => {
            setDestination(loc.name);
            setDestLat(String(loc.lat));
            setDestLng(String(loc.lng));
          }}
          placeholder="Search a place…"
        />
      </Field>
      <div className="grid grid-cols-2 gap-2">
        <Field label="Dest lat">
          <input
            className="input"
            value={destLat}
            onChange={(e) => setDestLat(e.target.value)}
            placeholder="-2.43"
            required
          />
        </Field>
        <Field label="Dest lng">
          <input
            className="input"
            value={destLng}
            onChange={(e) => setDestLng(e.target.value)}
            placeholder="34.83"
            required
          />
        </Field>
      </div>

      <div className="grid grid-cols-3 gap-2">
        <Field label="Distance (km)">
          <input
            className="input"
            type="number"
            value={distanceKm}
            onChange={(e) => setDistanceKm(e.target.value)}
          />
        </Field>
        <Field label="Duration (min)">
          <input
            className="input"
            type="number"
            value={durationMin}
            onChange={(e) => setDurationMin(e.target.value)}
          />
        </Field>
        <Field label="Surface">
          <select
            className="input"
            value={surfaceType}
            onChange={(e) => setSurfaceType(e.target.value as SurfaceType)}
          >
            <option value="paved">Paved</option>
            <option value="gravel">Gravel</option>
            <option value="track">Track</option>
          </select>
        </Field>
      </div>

      {saveError && <p className="text-[11px] text-destructive">{saveError}</p>}
      <div className="flex gap-2 pt-1">
        <button
          type="submit"
          disabled={saving}
          className="flex-1 rounded-md bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground hover:opacity-90 disabled:opacity-60"
        >
          {saving ? 'Saving…' : 'Add route'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-border px-3 py-1.5 text-xs font-medium text-muted-foreground hover:bg-muted"
        >
          Cancel
        </button>
      </div>
      <style>{`.input{width:100%;border:1px solid hsl(var(--border));background:hsl(var(--background));border-radius:6px;padding:4px 8px;font-size:12px;color:hsl(var(--foreground));}`}</style>
    </form>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-0.5">
      <span className="text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </span>
      {children}
    </label>
  );
}
