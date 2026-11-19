// Browser-only adapter for loading the Google Maps JS API.
// Isolated here so the rest of the app stays free of window/document access.

import { GOOGLE_MAPS_BROWSER_KEY } from '@/config';

let loadPromise: Promise<typeof google> | null = null;

// Google checks the key only after the script has loaded, so a rejected key can't reject loadPromise.
// It calls window.gm_authFailure instead; the maps subscribe here to show a message rather than a grey box.
const AUTH_FAILURE_MESSAGE =
  'Google Maps rejected the browser key for this site. In Google Cloud Console, add this URL ' +
  `(${typeof window === 'undefined' ? '' : window.location.origin}/*) to the key's website restrictions.`;
let authFailed = false;
const authFailureListeners = new Set<(message: string) => void>();

declare global {
  interface Window {
    __initSafariMap?: () => void;
    gm_authFailure?: () => void;
    google: typeof google;
  }
}

/** Calls `onFailure` when (or if already) Google rejects the key; returns the unsubscribe function. */
export function onGoogleMapsAuthFailure(onFailure: (message: string) => void): () => void {
  if (authFailed) onFailure(AUTH_FAILURE_MESSAGE);
  authFailureListeners.add(onFailure);
  return () => {
    authFailureListeners.delete(onFailure);
  };
}

export function loadGoogleMaps(): Promise<typeof google> {
  if (typeof window === 'undefined') {
    return Promise.reject(new Error('Google Maps can only load in the browser'));
  }
  if (window.google?.maps) return Promise.resolve(window.google);
  if (loadPromise) return loadPromise;

  const key = GOOGLE_MAPS_BROWSER_KEY;
  if (!key) {
    return Promise.reject(
      new Error('Missing Google Maps browser key: set VITE_GOOGLE_MAPS_BROWSER_KEY in web/client/.env'),
    );
  }

  window.gm_authFailure = () => {
    authFailed = true;
    authFailureListeners.forEach((listener) => listener(AUTH_FAILURE_MESSAGE));
  };

  loadPromise = new Promise((resolve, reject) => {
    window.__initSafariMap = () => resolve(window.google);
    const s = document.createElement('script');
    s.async = true;
    s.defer = true;
    s.src =
      `https://maps.googleapis.com/maps/api/js?key=${key}` +
      `&loading=async&libraries=geometry,places&callback=__initSafariMap`;
    s.onerror = () => reject(new Error('Failed to load Google Maps script'));
    document.head.appendChild(s);
  });

  return loadPromise;
}
