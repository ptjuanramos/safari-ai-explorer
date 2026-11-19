# Data contract

The Python jobs write these files; the .NET adapters (`web/src/SafariExplorer.Infrastructure/JobData`) read them. Field names are `snake_case` in the files. The API returns its own `camelCase` DTOs. Missing numbers are JSON `null`, never `NaN`.

The same layout is used in a folder (`DATA_DIR`, `Storage:Kind` = `Files`) and in the blob container `job-data` (`Storage:Kind` = `Azure`, ADR-0004): a file's path below `DATA_DIR`, with `/`, is its blob name.

```
DATA_DIR/
├─ road-risk/<place-slug>/
│  ├─ state.json               jobs' progress: the last complete rain day
│  ├─ latest.json              newest run (complete or not), for the API
│  └─ <YYYY-MM-DD>/            one run per rain day (the last day of the rain window)
│     ├─ edges.geojson
│     └─ manifest.json         written last; a folder without it is ignored
└─ sightings/
   ├─ latest.json
   └─ <YYYY-MM-DD>/            one run per day of sightings-refresh
      ├─ <species>/<sigma_m>m/<tx>_<ty>.json   one tile per species × kernel width × tile with data
      └─ manifest.json
```

`<place-slug>` is the OSM place name lower-cased with non-alphanumerics replaced by `-`, for example `tarangire-national-park-tanzania`.

## latest.json

```json
{"run": "2026-11-14", "manifest": "2026-11-14/manifest.json", "updated_at": "2026-11-15T19:30:33+00:00"}
```

It is replaced atomically after the run's manifest is written, and it only moves forward.

## Road risk

### state.json

```json
{"complete_through": "2026-11-13", "updated_by": "road-risk-backfill", "updated_at": "2026-11-15T20:00:00+00:00"}
```

- `complete_through` is the newest rain day whose run is complete. It only moves forward. Both jobs advance it after each complete run.
- `road-risk-refresh` computes every day from `complete_through + 1` to the newest IMERG day. Without a state file it computes only the newest day.
- A partial day is written (`complete: false`) but doesn't move the state, so the next refresh redoes it.

### manifest.json

| Field | Meaning |
|---|---|
| `job` | `road-risk-refresh` or `road-risk-backfill` |
| `place` | OSM place name |
| `date` | The run's rain day (the folder name): the last day of the rain window |
| `provider` | `IMERG` |
| `lookback_days` | Days in the decayed rain window, ending at `date` |
| `edge_count` | Number of features in `edges.geojson` |
| `complete` | `true` when every day in the window has all 48 half-hour granules |
| `granules_per_day`, `granule_coverage` | 48, and the granules found per window day. A low count on the last day means its rain is undercounted. |
| `note` | What the numbers mean and that they're unvalidated |
| `files`, `written_at` | Files in the run and when the manifest was written |

### edges.geojson

A GeoJSON `FeatureCollection` in **EPSG:4326** (lon, lat; display only, per ADR-0001). One `LineString` feature per OSM edge, with `id` = `"<u>-<v>-<key>"`.

| Property | Meaning |
|---|---|
| `road_type` | OSM `highway` value (the first one when OSM lists several, which is the one the model uses) |
| `length_m` | Edge length in meters |
| `travel_time_m` | Travel time in **minutes** at OSMnx's imputed speed |
| `rain_mm` | Decayed rain index (Σ rain × 0.84^days_ago) of the wettest pixel the edge touches; `null` if it touches none |
| `surface_risk` | `min(1, base_road_risk + road_vulnerability × rain_risk)`, 0 to 1 |

## Sightings

### Layers and zoom

- There is one layer per kernel width σ: 10 km, 5 km, 2 km, 1 km, 500 m and 250 m. Cells are σ/2.
- The UI picks the layer whose σ is closest to **max(250 m, 30 px × meters per screen pixel)**. That's the rule from the exploration notebook's map: roughly z9 → 10 km, z10 → 5 km, z11 → 2 km, z12 → 1 km, z13 → 500 m, and z14 or closer → 250 m.
- **Precision filter (layers of 2 km and below):** these layers use only records with `coordinateUncertaintyInMeters` ≤ σ. A record with unknown uncertainty counts as precise. The 10 km and 5 km layers use every record.
  - Why: 91–98% of the buffalo, elephant, lion, cheetah and leopard records are iNaturalist observations whose location is hidden. iNaturalist moves the point to a random spot inside a 0.2° cell (about 22 km across), and the record's uncertainty is about 31 km.
  - The fine layers are therefore mostly empty for those species: in the 2026-11-15 run, 21 lion, 9 leopard and 8 cheetah records were precise enough.
  - **The UI should say so**, using the layer's `species_records`. GBIF can't show these animals at street level.
  - The share's beta prior uses the layer's own species average (`species_p0`), because the filter changes the mix.

### Tiles

- Each layer's grid covers the whole box in EPSG:32736 and is cut into tiles of `tile_cells` × `tile_cells` cells (256).
- Tile `(tx, ty)` holds grid columns `tx·256 …` and rows `ty·256 …`, counted from the south-west.
- Only tiles with at least one cell of `n ≥ min_records` are written. They are the same tiles for every species, because `n` counts all species.

### manifest.json

| Field | Meaning |
|---|---|
| `job`, `date` | `sightings-refresh` and the run date (today, UTC) |
| `years` | `[start_year, end_year]` of the GBIF records used |
| `bounds_wgs84` | Box as west, south, east, north |
| `crs` | `EPSG:32736`, the CRS of the grids |
| `records` | Mammal records with coordinates (before the precision filter) |
| `settings` | `sigmas_m`, `cell_fraction` (cell = σ × fraction), `tile_cells`, `uncertainty_filter_max_sigma_m` (2,000), `prior_records`, `min_records`, `confidence_records` |
| `layers` | Per σ: `sigma_m`, `cell_m`, `tile_cells`, `max_uncertainty_m` (`null` = not filtered), `records` and `species_records` (after the filter), `grid` (`x0`, `y0`, `nx`, `ny` of the whole-box grid), `species_p0`, and `tiles` (`tx`, `ty`, `bounds_wgs84`) |
| `species` | Per species name: `species_key`, `records`, and the colour scale `p0` (species average) and `vmax` (95th percentile of the 5 km share), both from the 5 km layer's records. One scale per species, so colours don't jump between zooms. |
| `meaning` | "where visitors see", not where the animal lives |

### `<species>/<sigma_m>m/<tx>_<ty>.json`

| Field | Meaning |
|---|---|
| `species`, `sigma_m`, `cell_m`, `crs`, `tx`, `ty` | What the tile is |
| `x0`, `y0` | UTM coordinates (EPSG:32736, meters) of the **centre** of the tile's south-west cell |
| `nx`, `ny` | Columns and rows (at most `tile_cells`; edge tiles are smaller) |
| `bounds_wgs84` | The box the tile's cells cover |
| `p0` | The layer's species average, used by the prior |
| `confidence_records` | Fade colour by `n / (n + confidence_records)` |
| `share` | Effort-adjusted share with the beta prior, `null` where `n < min_records` |
| `n` | Weighted records of all species around the cell |
| `lo`, `hi` | Approximate 95% range of `share`, `null` where `share` is |

- Arrays are flat and row-major from the south: cell `(row, col)` is at index `row * nx + col`, with its centre at `(x0 + col × cell_m, y0 + row × cell_m)`.
- A UI on lon/lat tiles must project these centres from UTM, for example with proj4 and `+proj=utm +zone=36 +south +datum=WGS84 +units=m +no_defs`.

## User paths

Unlike everything above, saved paths are written by the **API**, not by a job (ADR-0003). There is one file per path (`Files`) or one table row per path (`Azure`, ADR-0004), and no manifest or `latest.json`.

```
DATA_DIR/user-paths/<id>.json         Storage:Kind = Files
table userpaths, row (trip, <id>)     Storage:Kind = Azure
```

```json
{
  "version": 2,
  "id": "3f2b9c0d4e5a46b7a8c9d0e1f2a3b4c5",
  "route_number": 2,
  "origin": "Tarangire Gate",
  "destination": "Silale Swamp",
  "stops": [{"lat": -3.7167, "lng": 36.0167}, {"lat": -4.0, "lng": 36.08}],
  "surface_type": "track",
  "distance_km": 42.5,
  "duration_min": 95,
  "created_at": "2026-11-18T09:30:00+00:00"
}
```

| Field | Meaning |
|---|---|
| `version` | Format version, 2. Version 1 called `route_number` `day_number` (column `DayNumber`); both are still read. |
| `id` | 32 lowercase hex digits, also the file name |
| `route_number` | The route the path belongs to, 1 to 60. The UI groups paths by it; it is not a day or a date. |
| `origin`, `destination` | Place names, 1 to 120 characters |
| `stops` | 2 to 27 points (WGS84) the driving route passes, in order: origin first, destination last. The road itself comes from Google directions and isn't stored. |
| `surface_type` | `paved`, `gravel` or `track` (the user's choice) |
| `distance_km`, `duration_min` | As entered by the user |
| `created_at` | When the API saved it. Paths of the same route are listed in this order. |

- Each file is written to a temp file (`.<id>.<random>.tmp`) and renamed. Readers skip files whose name isn't a slug, and files that don't parse.
- **As a table row** (`Azure`): `PartitionKey` = `trip`, `RowKey` = `id`, and the columns `Version`, `RouteNumber`, `Origin`, `Destination`, `Stops`, `SurfaceType`, `DistanceKm`, `DurationMin`, `CreatedAt` hold the fields above. `Stops` is the `stops` array as a JSON string (a column can't hold a list). Rows that don't parse are skipped.
- The API returns these as `camelCase` from `GET /api/user-paths`.
