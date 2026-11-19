# client

The React SPA (Vite, React 19, Tailwind 4, shadcn/ui). `npm run build` writes `dist/`, which the web Docker image copies into the API's `wwwroot`, so the API serves the UI and `/api` from one origin.

## Run

```bash
npm install
npm run dev        # http://localhost:5173, proxies /api to the API on http://localhost:5201
npm run typecheck
npm run build
```

Start the API first (`dotnet run --project ../src/SafariExplorer.Api`), or use the API's `spa` launch profile, which starts `npm run dev` for you.

## Settings (`web/client/.env`, git-ignored)

| Variable | What |
|---|---|
| `VITE_GOOGLE_MAPS_BROWSER_KEY` | Google Maps JavaScript API browser key (maps and place search). It ships to the browser, so restrict it to your domains. The old `VITE_LOVABLE_CONNECTOR_GOOGLE_MAPS_BROWSER_KEY` name also works. |
| `VITE_API_BASE_URL` | Optional. Empty means the same origin. |

Driving directions don't use this key: the API calls Google Routes with its own server key (`GOOGLE_MAPS_API_KEY`). Without it, routes are drawn as straight lines between the stops and only match park roads where those lines happen to lie on them.

## One map

Everything is drawn on the road-trip map (`components/RoadTrip`, `Sidebar`, `Map`, `BottomBar`), so routes and sightings can be read together.

- **Paths** (`hooks/useTripData.ts`): the trip's drives, saved by the API in `DATA_DIR/user-paths/` (ADR-0003).
  - It loads them from `GET /api/user-paths`, adds one with `POST` (from the sidebar's form) and deletes one with `DELETE` (the × on a day card).
  - A path stores only its stops (origin and destination). `useDrivingRoutes` gets the road between them from `POST /api/directions`, and a 503 or other failure is shown in the header.
- **Road risk:** `useRoadRisk` sends the routes and the departure date to `POST /api/road-risk/routes`, which scores them against every park with data (one run per park).
  - Routes and cards show the API's **surface risk** directly (0–100%): low up to 30%, medium up to 60%, high above. `lib/roadRisk.ts` holds those bands and the vehicle, warning and confidence rules; they are presentation choices.
  - A route that doesn't run on any mapped park road (Serengeti, Ngorongoro, Tarangire, Lake Manyara, Arusha) has no estimate and is drawn **grey**.
  - The map's 🗺 toggle draws every park's roads coloured by risk (`GET /api/road-risk/{rain day}?place=`, the parks from `GET /api/road-risk/places`).
- **Sightings:** the map's 🦁 toggle adds the heat map and a panel (`Map/SightingsPanel.tsx`) to pick the species.
  - `useSightingsRun` reads `GET /api/sightings/latest` the first time the layer is on, and `useSightingsOverlay` picks the layer for the zoom (`lib/sightings.ts`, the notebook's rule).
  - It loads only the tiles in view (`GET /api/sightings/{species}/{sigma}/{tx}/{ty}`) and draws each as a ground overlay.
  - The colour runs from the species average to its 95th percentile and fades with the number of records.

Endpoints: `../src/SafariExplorer.Api/SafariExplorer.Api.http`. File formats: `../../docs/data-contract.md`.
