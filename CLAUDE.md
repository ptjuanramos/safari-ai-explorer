# Safari AI Explorer

Two features for a safari app in northern Tanzania, each produced by its own job:

1. **`sightings-refresh`:** where visitors see buffalo, elephant, lion, cheetah and leopard. Built from GBIF sightings only.
2. **`road-risk-refresh`:** how likely each park road is to be muddy or hard to pass, given the rain of the last days, for the northern parks listed in `jobs/settings.toml` (Serengeti, Ngorongoro, Tarangire, Lake Manyara, Arusha). Built from OpenStreetMap roads and Earth Engine rainfall.

Both started in the exploration repo `../safari-ai-explorer-notebook`. The sightings come from `animals-sightnings/` (notebook 14), and the road risk from `integration/` (notebooks 02 and 05).

Open issues live in [README.md](README.md). Coding conventions live in [docs/conventions.md](docs/conventions.md):

@docs/conventions.md

## Architecture

- **Python jobs** (`jobs/`) compute data and write it as files to a shared volume (`data/`). They never draw maps.
- **.NET 11 API** (`web/`) reads those files through repository interfaces and serves them, from the folder or from an Azure Storage account (ADR-0004; the jobs still write folders, so their files are uploaded by hand).
- **React SPA** (`web/client/`, Vite + React ^19.2.0, Google Maps) displays them on one map: the trip's saved paths with their road risk, and the sightings heat map as a layer (🦁 toggle with a species picker), so both can be read together. The UI design is the user's (built in Lovable); it was turned from TanStack Start (SSR) into a static SPA the API serves. Code tour in [web/client/README.md](web/client/README.md).

**The Python code is map-agnostic.** A job exports results (for example the Gaussian kernel shares), and the API and UI decide how to show them. There is no map code in Python; the UI draws.

## Scope: keep it simple

- **The heat map stays GBIF-only.** The exploration repo also tried habitat models (AlphaEarth embeddings, NDVI, XGBoost). They mostly learned where visitors go, and a model that only knew the coordinates did as well. Don't bring that back unless the user asks.
  - The heat map shows where visitors *see* an animal, not everywhere it lives. Keep that wording in anything user-facing.
- **The road risk is a rule-based model**, not a trained one. The road weights and rain thresholds are hand-set by the user, so treat them as the user's design decisions.
- **Honesty:** GBIF records are visitor-biased and the road risk is unvalidated. Say what a map can and can't show.


The file formats are in [docs/data-contract.md](docs/data-contract.md). Change the Python writer and the .NET reader together.

## Layout

```
jobs/                  Python jobs (package `jobs`), tests; code tour in jobs/README.md
web/                   .NET 11 solution (SafariExplorer.slnx) + client/
docs/                  ADRs, conventions, data contract
data/                  job output (git-ignored), mounted as the shared volume
docker-compose.yml     road-risk-refresh, sightings-refresh, road-risk-backfill (manual), web
infra/                 main.bicep (subscription scope: resource group) → resources.bicep (storage account + App Service);
                       the names and SKU in main.bicepparam; one-time setup in infra/README.md
.github/workflows/     provision.yml (by hand: what-if + Bicep deploy), deploy.yml (push to main under web/: test, build, deploy)
```

### Python (`jobs/src/jobs/`)

One package, one subpackage per feature. [jobs/README.md](jobs/README.md) walks through the code in reading order. Imports are absolute (`from jobs.shared.outputs import ...`).

| Module | What it holds |
|---|---|
| `shared/outputs.py` | `write_file` / `write_manifest` / `write_run` (files first, `manifest.json` last), `update_latest` (atomic `latest.json`, optionally only forward), `read_state` / `advance_state` (`state.json`, only forward), `run_exists`, `slug` |
| `shared/cli.py` | `parser` / `add_road_risk_args` (flags with env fallbacks), `parse_date`, `today_utc`, `init_earth_engine`, `use_osmnx_cache` |
| `shared/crs.py` | `UTM` (EPSG:32736), `WGS84` |
| `shared/geo.py` | `Geo` (static helpers): `with_utm_xy` (drops records without coordinates, adds `x`, `y` in meters), `wgs84_to_utm_bounds`, `utm_to_wgs84_bounds` |
| `sightings/config.py` | `SightingsConfig.load(settings.toml)`: `species` (name → GBIF speciesKey), `taxon_key` (359 = Mammalia), `bounds` (west, south, east, north), `start_year`, `end_year`, `cache_dir` |
| `sightings/service.py` | `SightingsService` (GBIF today; `base_url`, `page_size`, `retries`, `wait_s`, `timeout_s`, `chunk_bytes`, `max_records`, `min_box_deg` are fields): `fetch_year(taxon_key, bounds, year)` → `RECORD_COLUMNS`, `boxes_under_limit`, `count_records`, `halves`, `fetch_all`, `get_page`, `search_params` |
| `sightings/sightings.py` | `Sightings(config, service)`: `fetch(current_year)` (one year at a time; older years cached as `sightings_<taxon>_<year>_<box>.parquet`, the last `refetch_years` = 2 always fetched again because records arrive late; a cache file with other columns is deleted and fetched again), `get_cache_path` |
| `sightings/models.py` | `RECORD_COLUMNS`, `SightingsGridSettings` (six `sigmas_m`, `tile_cells`, `uncertainty_filter_max_sigma_m`, prior/min/confidence records; `cell_m`, `max_uncertainty_m`), `ShareTile` (fields documented) |
| `sightings/kernel.py` | `KernelShare` (the calculation, static/class methods): `gaussian_weights`, `weighted_counts` (n, k), `beta_share`, `share_range` (95%), `species_average` (p0), `scale_max` (vmax) |
| `sightings/records.py` | `LayerRecords`: `precise` (uncertainty ≤ σ), `for_layer` (UTM + filter only up to 2 km) |
| `sightings/grid.py` | `TileGrid` (whole-box UTM cell centres cut into tiles: `covering`, `tiles`), `ShareTileBuilder(species, bounds, settings).build(records, sigma_m)` → yields a `ShareTile` per species and tile with data |
| `sightings/refresh.py` | `sightings-refresh`: `tile_payload`, `colour_scale`, `run_layer` (writes each tile as it's computed), `run_sightings`, `main` |
| `road_risk/network.py` | `ParkRoadNetwork(place_name).load()` → OSMnx nodes and edges (`road_type`, `length_m`, `travel_time_m`), without footways and paths |
| `road_risk/rainfall.py` | `AntecedentPrecipitationIndex` + `DailyObservation`, `decayed_rain`; `RainGridProvider` (shared `frame_grid`, `latest_date`, `get_accumulated_precipitation`, `get_decayed_precipitation`) with `IMERGProvider` (0.1°, `granule_coverage`) |
| `road_risk/surface_risk.py` | `RainfallRiskModel` (mm → 0/0.3/0.6/1), `RoadRainVulnerability` (road type → factor), `EdgeRiskEnricher.enrich(edges, rain_pixels)` |
| `road_risk/config.py` | `RoadRiskConfig.load(settings.toml)`: `places` (OSM place names); `places_to_run(settings, only)` (just `--place` when given, else every place; settings read only then) |
| `road_risk/pipeline.py` | `park_network(place)` (built once per process, so a backfill reuses it), `compute_road_risk(place, day, lookback_days, provider)` → nodes, enriched edges (copies); `export_edges` (contract columns, EPSG:4326) |
| `road_risk/runs.py` | Shared by both road-risk jobs: `job_dir`, `day_range`, `rain_window`, `run_road_risk` (writes the run, moves `latest.json` and `state.json`), `is_complete`, `complete_through`, `latest_rain_day` |
| `road_risk/refresh.py` | `road-risk-refresh`: `days_to_refresh`, `refresh_place`, `main` (every place, one shared `IMERGProvider` so each day's granule count is asked once; a failing place is reported and the rest still run, exit 1) |
| `road_risk/backfill.py` | `road-risk-backfill`: `backfill` (one place), `default_range` (last 30 days), `main` (every place, same failure rule) |

Application settings live in [jobs/settings.toml](jobs/settings.toml) (`[road_risk]`: places; `[sightings]`: species, taxon, box over Tanzania, start year), read with `tomllib`; `--settings` / `SETTINGS_FILE` override the path (the Docker image uses `/app/settings.toml`).

`jobs/tests/` mirrors the package (`shared/`, `sightings/`, `road_risk/`). The tests need no network: GBIF and Earth Engine are monkeypatched, `tests/road_risk/conftest.py` provides `FakeIMERG`, fake edges and the `computed` fixture, and `tests/sightings/conftest.py` loads the real `settings.toml` (`config`, `species`). pytest runs with `--import-mode=importlib`, so test file names can repeat across folders. `jobs/cache/` (git-ignored) holds the GBIF parquet files and OSMnx's Overpass responses.

### .NET (`web/`)

Clean-architecture layers. Dependencies point inward: Api → Application → Domain, and Infrastructure → Application/Domain.

| Project | What it holds |
|---|---|
| `SafariExplorer.Domain` | `RoadRiskRun` (with `Complete`), `RoadSegmentRisk`, `RoadRiskSnapshot`, `TripRoute`, `RouteRisk`, `RouteRiskReport`, `DrivingRoute`, `SightingsRun` (layers with their tile index; `HasTile`), `SightingsLayer`, `SightingsTile`, `UserPath`, `RoadRiskPlace`; `RouteRisk.Places`, `RouteRiskReport.Runs` (no dependencies) |
| `SafariExplorer.Application` | Ports `IRoadRiskRepository`, `ISightingsRepository`, `IUserPathRepository`, `IRouteMatcher`, `IDirectionsService`; use cases `GetRoadRiskByDate`, `ListRoadRiskDates`, `GetRouteRisk` (one place, or every place when null: per place the latest run for today or later, else the newest within `MaxRunAgeDays` = 7 before the date; their edges merged by id so a shared border road counts once; each route tagged with the places it touches), `ListRoadRiskPlaces`, `GetDrivingRoute`, `GetLatestSightingsRun`, `GetSightingsTile`, `ListUserPaths` (by route number, then added order), `AddUserPath` (id + time from `TimeProvider`; `null` once `MaxPaths` = 200 are saved), `DeleteUserPath` (one `ExecuteAsync` each; `null` = not found); `NewUserPath.Validate()` |
| `SafariExplorer.Infrastructure` | `JobData/`: `IJobStore` (`ReadJsonAsync`, `ExistsAsync`, `ListKeysAsync`; keys use `/`), `RoadRiskRepository` and `SightingsRepository` over it (`latest.json` → `manifest.json` → files; parsed sightings manifests cached 10 min), `JobFiles` (slug and key checks prevent path traversal; a park's runs come from one listing); `FileSystem/`: `LocalJobStore` (`DATA_DIR`), `FileUserPathRepository` (`user-paths/<id>.json`, temp file + rename); `AzureStorage/`: `BlobJobStore`, `TableUserPathRepository` (partition `trip`, row key = id, stops as a JSON string), `AzureStorageClients` (connection string, else account URL + `DefaultAzureCredential`); `StorageOptions` (`Kind` Files/Azure, env `STORAGE_KIND`, `AZURE_STORAGE_CONNECTION_STRING`, `STORAGE_ACCOUNT_URL`); `Spatial/BufferRouteMatcher` (NetTopologySuite + ProjNet in EPSG:32736: an edge lies along a route when ≥ 50% of it is within 30 m; two-way roads counted once; risk weighted by length along the route); `Google/GoogleRoutesClient` (Routes API, points snapped with the Roads API; key from `GoogleMaps:ApiKey` or `GOOGLE_MAPS_API_KEY`); `AddJobData` (picks the adapters by `Storage:Kind`, validated at start), `ImportUserPathsAsync`, `AddGoogleDirections` |
| `SafariExplorer.Api` | Minimal API endpoints → use case → DTO (GeoJSON for roads); SPA static files + fallback; `RoadRisk:DefaultPlace` (only for `/dates` and `/{date}` without `?place`) |

Endpoints: `GET /api/road-risk/places` (each park with data and its newest rain day), `GET /api/road-risk/dates`, `/api/road-risk/{date}` (GeoJSON of every park road), `POST /api/road-risk/routes` (body `{date, routes: [{id, path: [{lat, lng}]}]}` → `runs` (one per park used), and per route its coverage, length-weighted mean and max surface risk and `places`; every park unless `?place=`; 404 when no park has a recent enough run) (each takes `?place=<slug>`), `POST /api/directions` (`{origin, destination, intermediates}` → encoded polyline; 503 without a Google key), `GET /api/sightings/latest` (the run with every layer and its tile index, `bounds_wgs84` per tile), `/api/sightings/{species}/{sigmaM}/{tx}/{ty}` (one tile; the UI asks only for the tiles in view). A tile is served only if the latest manifest lists it, which also stops path traversal. `GET /api/user-paths`, `POST /api/user-paths` (`{routeNumber, origin, destination, stops: [{lat, lng}], surfaceType, distanceKm, durationMin}` → 201; 400 with the reason; 409 when full), `DELETE /api/user-paths/{id}` (204 / 404).

To store data elsewhere later, add a new adapter next to `FileSystem/` and `AzureStorage/` that implements the same ports (or a new `IJobStore`) and pick it in `AddJobData`. Use cases and endpoints don't change.

Tests: `Application.Tests` (use cases with fakes), `Infrastructure.Tests` (repositories on `LocalJobStore` over a temp folder, user paths, the manifest cache, the route matcher, the Google client with a fake HTTP handler; `AzuriteTests` run the blob store, the table repository and the import against Azurite and are skipped when it isn't running), `Api.Tests` (`WebApplicationFactory`, including which adapters `Storage:Kind` picks).

## How the heat map works (`sightings-refresh`)

- **Effort-adjusted share:** (weighted records of species X) / (weighted records of all species) around each pixel. The weights are a Gaussian kernel in UTM meters.
- **Layers per zoom:** one layer per kernel width (`sigmas_m` 10 km, 5 km, 2 km, 1 km, 500 m, 250 m; cells of σ/2), matching zooms 9 to 14 of the exploration notebook's map. The UI picks the layer closest to max(250 m, 30 px × meters per pixel).
- **Tiles:** each layer's whole-box UTM grid is cut into 256 × 256-cell tiles, and only tiles with data are written. The manifest lists them with their `bounds_wgs84`, so the UI loads only the tiles in view. A 250 m layer has 125 m cells, about 1,800 × 1,800 across the box.
- **Precision filter (σ ≤ 2 km only, `uncertainty_filter_max_sigma_m`):** these layers use only records with `coordinateUncertaintyInMeters` ≤ σ (unknown counts as precise). The 10 km and 5 km layers use every record.
  - 91–98% of the five species' records are iNaturalist observations with the location hidden (moved randomly inside a 0.2° cell; uncertainty about 31 km).
  - So the fine layers are mostly empty for them: about 21 lion, 9 leopard and 8 cheetah records are precise enough.
  - The manifest's per-layer `species_records` lets the UI say so. GBIF can't show these animals at street level.
  - Even the 5 and 10 km layers carry that ±11 km noise.
  - The prior uses the layer's own species average.
  - **Later idea:** spread each hidden record evenly over its 0.2° cell instead of dropping it.
- **Avoiding false hotspots:**
  - A beta prior (`prior_records`) pulls thin areas toward the species average.
  - Colour should fade with confidence, n / (n + `confidence_records`). The grid carries `n` and `confidence_records`.
  - Cells below `min_records` are `null`.
- **Colour scale:** runs from the species average (`p0`) to the 95th percentile of the 5 km smoothed share (`vmax`), both from the 5 km layer's records, one scale per species for every zoom.
- **Range:** each cell has `lo` / `hi`, an approximate 95% range.
- **Bias:** this is where visitors see each animal, with most of the "more tourists" effect removed, not a population survey.

## How the road surface risk works (`road-risk-refresh`)

1. **Roads:** for each place, `ParkRoadNetwork` loads the OSMnx graph (`network_type="all"`), adds speeds and travel times, and drops `footway`/`path`.
2. **Rain grid:** `frame_grid(edges.total_bounds)` builds pixel boxes aligned to the dataset resolution, in EPSG:4326, because the boxes must match the dataset's native degree pixels.
3. **Rain index:** `get_decayed_precipitation(grid, date, lookback_days)` sums daily rain × k^days_ago with k = 0.84 + 0.012 × (t_clt − t_avg). The temperatures are fixed at 20 °C, so k = 0.84.
   - It raises an error if Earth Engine returns a different number of days than asked, or if `lookback_days < 1`.
4. **Joining rain to roads:** `EdgeRiskEnricher` joins edges to pixels in UTM (ADR-0001), and each edge takes the wettest pixel it touches.
5. **Risk score:** `surface_risk = min(1, base_road_risk + road_vulnerability × rain_risk)`.
6. **Dates:**
   - **A run is named after its rain day**: the last day of its rain window. Both jobs use IMERG only (CHIRPS lags weeks and was removed).
   - **Completeness:** the manifest records each window day's granule count out of 48, and `complete` is true when all are 48. The newest IMERG day is usually partial, which undercounts the day that weighs most.
   - **`state.json`** holds `complete_through`, the last complete rain day. Both jobs advance it after a complete run, and it never moves backwards. It's a plain file so it can become a database table later.
   - `road-risk-refresh` (scheduled) computes every day from `complete_through + 1` to the newest IMERG day (only the newest day if there's no state yet). A partial day is written for the map but doesn't move the state, so the next run redoes it. When nothing is newer it exits 0 and the next run retries.
   - `road-risk-backfill` (manual) defaults to the last 30 days, ending at the newest IMERG day. It skips complete runs unless `--overwrite` and redoes incomplete ones. Run it once first so the refresh has a state to continue from.
   - `latest.json` points at the newest run, complete or not, and never moves backwards.

**Check after the move:** `road-risk-backfill --start 2024-04-30 --end 2024-04-30 --lookback-days 5` gives 2,207 edges for Tarangire (the `integration/` baseline). The edge count comes from the road network, so it doesn't depend on the rain provider.

## Setup and commands

```bash
python -m venv .venv
.venv/Scripts/python -m pip install -e "jobs[dev]"
.venv/Scripts/python -m pytest -q jobs/tests
(cd jobs && ../.venv/Scripts/ruff format src tests && ../.venv/Scripts/ruff check src tests)
dotnet test web
```

- **Platform:** Windows (PowerShell or Git Bash).
- **The current `.venv`** is at the repo root and uses `--system-site-packages`, with `osmnx` installed into the venv.
- **Running jobs locally** (from the repo root):
  - `.venv/Scripts/road-risk-refresh --data-dir data --cache-dir jobs/cache --ee-project <id>` (every place in `settings.toml`; `--place "Tarangire National Park, Tanzania"` for one)
  - `.venv/Scripts/sightings-refresh --data-dir data --cache-dir jobs/cache`
  - `.venv/Scripts/road-risk-backfill --data-dir data --cache-dir jobs/cache --ee-project <id>` (last 30 days; `--start` / `--end YYYY-MM-DD` to choose)
  - Each flag falls back to an env var: `DATA_DIR`, `CACHE_DIR`, `EE_PROJECT`, `PLACE`, `LOOKBACK_DAYS`, `SETTINGS_FILE`.
- **Running the API locally:** `dotnet run --project web/src/SafariExplorer.Api` (profile `http` reads `../../../data`). Against the storage account: set `STORAGE_KIND=Azure` and `STORAGE_ACCOUNT_URL=https://<account>.blob.core.windows.net` (after `az login`), or `AZURE_STORAGE_CONNECTION_STRING` (Azurite: `UseDevelopmentStorage=true`). Profile `spa` also starts the client's `npm run dev` through SpaProxy. The UI: `cd web/client && npm install && npm run dev` (http://localhost:5173, proxies `/api` to :5201); its browser key goes in `web/client/.env` as `VITE_GOOGLE_MAPS_BROWSER_KEY`. Directions need a server key with the Routes and Roads APIs: `GOOGLE_MAPS_API_KEY` (env or user secrets) on the API.
- **Docker:**
  - In `.env` set `EE_PROJECT`, `GOOGLE_MAPS_API_KEY` (optional, for directions) and `EE_CREDENTIALS_DIR` (the folder holding Earth Engine's `credentials` file, mounted read-only because `ee.Authenticate()` can't run in a container).
  - `docker compose run --rm road-risk-refresh`, `docker compose run --rm sightings-refresh`, `docker compose up web` (port 8080).
  - Before the first `up web`, create the user-paths folder in the volume (the subpath mount fails without it): `docker run --rm -v safari-data:/data alpine mkdir -p /data/user-paths`. In Git Bash prefix `MSYS_NO_PATHCONV=1`.
  - `docker compose run --rm road-risk-backfill` (last 30 days, or `--start … --end …`; profile `manual`, never started by `up`).
  - Containers share the external volumes `safari-data` (`/data`; the jobs write, `web` reads it read-only except `user-paths/`, mounted read-write as a volume `subpath`, Docker Engine 26+) and `safari-cache` (`/cache`). They are created by hand before the first run (`external: true`, so Compose won't create them) and appear in Docker Desktop's Volumes tab.
  - The web image runs `npm ci && npm run build` in `web/client/` and serves `dist/` from `wwwroot`. The browser key is read from `web/client/.env` at build time.
  - **Azurite** (local Azure Storage emulator, data in `.azurite/`, git-ignored): `docker compose --profile azurite up -d azurite` (blob 10000, table 10002). The `AzuriteTests` run only while it's up.
  - `web` takes `STORAGE_KIND`, `AZURE_STORAGE_CONNECTION_STRING`, `STORAGE_ACCOUNT_URL` from `.env` (default `Files`). There is no `az login` in the container, so `Azure` there needs a connection string.
  - **Later:** scheduling the refresh jobs (host cron or a scheduler container; suggested: `road-risk-refresh` 2 to 4 times a day, `sightings-refresh` weekly), and an Earth Engine service-account key instead of the mounted credentials.
- **Azure environment (ADR-0005):** `infra/main.bicep` creates the account, container `job-data`, table `userpaths`, the App Service and the role assignments; run it with the **Provision Azure** workflow, and **Deploy web app** ships `web/` on each push to `main`. One-time setup (resource group, GitHub OIDC identity, variables and secrets) in [infra/README.md](infra/README.md).
- **Storage account (ADR-0004):** shared-key access is off, so use `--auth-mode login` (connection strings only work for Azurite). Uploading needs **Storage Blob Data Contributor** (Bicep gives it to `AZURE_DATA_UPLOADER_ID`), and the web app's identity gets **Storage Blob Data Reader** on the container and **Storage Table Data Contributor** on the table.
  - Upload the job files (same layout; skip `user-paths/`): `az storage blob upload-batch -d job-data -s data/road-risk --destination-path road-risk --account-name <account> --auth-mode login`, and the same for `data/sightings/<run>` and `data/sightings/latest.json` (`az storage blob upload`). Upload the run before `latest.json`, as the jobs write them.
  - Copy the saved paths once: `dotnet run --project web/src/SafariExplorer.Api -- import-user-paths data` with `STORAGE_ACCOUNT_URL` (or the connection string) set. Running it again replaces the same rows.
- **Earth Engine:** `ee.Authenticate()` once; the jobs then call `ee.Initialize(project=...)` with `--ee-project` / `EE_PROJECT`.
  - The project is in the non-commercial **restricted mode**: calls still work but print a quota warning. Keep Earth Engine calls small, and backfill short ranges: every place costs its own rain calls per day (the granule counts are shared).
- **OSMnx** caches Overpass responses in `--cache-dir` (`jobs/cache/` locally).
  - The public Overpass server is often at capacity, so retry later or reuse the cache.
  - The cached Tarangire responses were copied from `integration/cache`. The other parks are downloaded on their first run (Serengeti is about 13,000 km²).
  - **Arusha NP:** OSM's only polygon is about 116 km² (Momella Lakes and Ngurdoto), not the whole 550 km² park with Mount Meru.

## Data notes

- **GBIF:** deep paging can time out or return 503. `SightingsService.get_page` retries with growing waits, and fetching one year at a time keeps offsets small.
  - GBIF serves pages past offset 10,000 at about 4 KB/s (2 hours for one year of the Tanzania box). So `fetch_year` first counts the year's records (`limit=0`, instant). If there are more than `max_records` = 10,000, `boxes_under_limit` cuts the box in halves along its longer side, again and again until each part fits. Then it fetches each part and drops duplicates by `gbifID`, because GBIF's ranges include both ends. 2019 took 4 parts (15,312 records).
  - `timeout_s` caps the whole request (the answer is streamed and the deadline checked per chunk): `requests`' own timeout only limits each wait between bytes, and a trickling GBIF answer once held a page open for 5 hours.
  - The box covers all of Tanzania and border strips of its neighbours, including the Masai Mara, because records are filtered by coordinates, not country. It holds about 140,000 mammal records for 2017–2026 (the old Serengeti box: about 19,600), so the GBIF fetch and the run are several times bigger.
- **CHIRPS** was dropped: its final product lags by a few weeks (on 2026-11-15 it ended at 2026-09-30).
- **IMERG** (`NASA/GPM_L3/IMERG_V07`) lags by about a day: on 2026-11-15 the newest half-hour was on 2026-11-14 (8 of 48). It checks the granule count for every day with a separate `getInfo` call, which is slow for long lookbacks.
- **Next ideas:**
  - seasonality for the heat map (add `month` to `COLUMNS`)
  - a GBIF publisher breakdown
  - wiring `risk_weight_minutes` into routing
  - validating road risk against ranger or guide reports
