// Browser-only adapter for Google Places API (New) autocomplete + place details.
import { loadGoogleMaps } from './googleMapsLoader';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
let placesLib: any = null;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
let sessionToken: any = null;

async function getPlacesLib() {
  const g = await loadGoogleMaps();
  if (!placesLib) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    placesLib = await (g.maps as any).importLibrary('places');
  }
  if (!sessionToken) sessionToken = new placesLib.AutocompleteSessionToken();
  return placesLib;
}

export interface PlaceSuggestion {
  placeId: string;
  label: string;
}

export async function searchPlaces(input: string): Promise<PlaceSuggestion[]> {
  const text = input.trim();
  if (!text) return [];
  const lib = await getPlacesLib();
  const { suggestions } = await lib.AutocompleteSuggestion.fetchAutocompleteSuggestions({
    input: text,
    sessionToken,
  });
  return suggestions
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .filter((s: any) => s.placePrediction)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    .map((s: any) => ({
      placeId: s.placePrediction.placeId,
      label: s.placePrediction.text?.toString?.() ?? '',
    }));
}

export async function getPlaceLocation(
  placeId: string,
): Promise<{ lat: number; lng: number; name: string } | null> {
  const lib = await getPlacesLib();
  const place = new lib.Place({ id: placeId });
  await place.fetchFields({ fields: ['location', 'displayName'] });
  // Start a fresh session after a billable details fetch.
  sessionToken = new lib.AutocompleteSessionToken();
  if (!place.location) return null;
  return {
    lat: place.location.lat(),
    lng: place.location.lng(),
    name: place.displayName ?? '',
  };
}
