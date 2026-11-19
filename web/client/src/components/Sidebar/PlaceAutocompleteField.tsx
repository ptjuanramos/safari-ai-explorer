import { useState } from 'react';
import { usePlacesAutocomplete } from '@/hooks/usePlacesAutocomplete';
import { getPlaceLocation } from '@/lib/placesAutocomplete';

interface Props {
  value: string;
  onChange: (name: string) => void;
  onResolved: (loc: { lat: number; lng: number; name: string }) => void;
  placeholder?: string;
}

export function PlaceAutocompleteField({ value, onChange, onResolved, placeholder }: Props) {
  const [focused, setFocused] = useState(false);
  const [picking, setPicking] = useState(false);
  const { suggestions, loading } = usePlacesAutocomplete(value, focused);

  const pick = async (placeId: string, label: string) => {
    onChange(label);
    setPicking(true);
    try {
      const loc = await getPlaceLocation(placeId);
      if (loc) onResolved({ ...loc, name: loc.name || label });
    } finally {
      setPicking(false);
      setFocused(false);
    }
  };

  const open = focused && (suggestions.length > 0 || loading);

  return (
    <div className="relative">
      <input
        className="input"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setTimeout(() => setFocused(false), 150)}
        placeholder={placeholder}
        autoComplete="off"
      />
      {picking && (
        <span className="absolute right-2 top-1/2 -translate-y-1/2 text-[10px] text-muted-foreground">
          …
        </span>
      )}
      {open && (
        <ul className="absolute z-20 mt-1 w-full max-h-48 overflow-auto rounded-md border border-border bg-popover shadow-md">
          {loading && (
            <li className="px-2 py-1.5 text-[11px] text-muted-foreground">Searching…</li>
          )}
          {suggestions.map((s) => (
            <li key={s.placeId}>
              <button
                type="button"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(s.placeId, s.label)}
                className="block w-full truncate px-2 py-1.5 text-left text-xs text-popover-foreground hover:bg-accent"
              >
                {s.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
