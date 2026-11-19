// Same origin by default: the .NET API serves the built SPA, and `npm run dev` proxies /api to it.
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';

// A browser key (restrict it to your domains in Google Cloud Console). The Lovable name still works.
export const GOOGLE_MAPS_BROWSER_KEY: string | undefined =
  import.meta.env.VITE_GOOGLE_MAPS_BROWSER_KEY ??
  import.meta.env.VITE_LOVABLE_CONNECTOR_GOOGLE_MAPS_BROWSER_KEY;
