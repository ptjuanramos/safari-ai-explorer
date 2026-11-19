# Jobs: code tour

Three command-line jobs that compute data and write it as files to `DATA_DIR`. They draw no maps: the API and UI read the files. The file formats are in [../docs/data-contract.md](../docs/data-contract.md).

| Job | When | Writes |
|---|---|---|
| `road-risk-refresh` | Scheduled, a few times a day | `road-risk/<place>/<rain day>/` for every IMERG day after `state.json` |
| `road-risk-backfill` | By hand | The same runs for a date range (default: the last 30 days) |
| `sightings-refresh` | Scheduled, weekly | `sightings/<date>/`: share tiles per species and kernel width |

## Layout

```
src/jobs/
├─ shared/                 used by both features
│  ├─ outputs.py           how a run is written: files → manifest.json → latest.json; state.json
│  ├─ cli.py               flags (with env-var fallbacks), today in UTC, Earth Engine and OSMnx setup
│  ├─ crs.py               EPSG:32736 (UTM, meters) and EPSG:4326
│  └─ geo.py               Geo: lon/lat → UTM x, y for records; bounds in both directions
├─ road_risk/
│  ├─ network.py           OSM roads for the park (OSMnx), without footways and paths
│  ├─ rainfall.py          decayed rain index; IMERG daily rain per pixel from Earth Engine
│  ├─ surface_risk.py      the rule-based model: rain → risk, road type → vulnerability, join to edges
│  ├─ pipeline.py          roads + rain + model → enriched edges; export to the contract columns
│  ├─ runs.py              one run for one day (manifest, completeness, latest.json, state.json)
│  ├─ refresh.py           road-risk-refresh
│  └─ backfill.py          road-risk-backfill
└─ sightings/
   ├─ config.py            SightingsConfig, loaded from settings.toml (species, taxon, box, years)
   ├─ service.py           SightingsService: the HTTP calls to GBIF (swap this to change source)
   ├─ sightings.py         Sightings.fetch: one year at a time, with the parquet cache
   ├─ models.py            RECORD_COLUMNS, SightingsGridSettings, ShareTile (every field documented)
   ├─ kernel.py            KernelShare: the maths (Gaussian weights, n and k, beta share, 95% range, p0, vmax)
   ├─ records.py           LayerRecords: which records go into a layer (precision filter up to 2 km)
   ├─ grid.py              TileGrid (cells → tiles) and ShareTileBuilder (puts it together per tile)
   └─ refresh.py           sightings-refresh
settings.toml              application settings (the species list lives here)
tests/                     same folders as src/jobs; no network needed
```

## How each job runs

**`road-risk-refresh`** ([road_risk/refresh.py](src/jobs/road_risk/refresh.py))

```
main
├─ runs.latest_rain_day          newest IMERG day (never after today)
├─ runs.complete_through         reads state.json: the last complete rain day
├─ days_to_refresh               every day after it, up to the newest
└─ for each day: runs.run_road_risk
   ├─ pipeline.compute_road_risk
   │  ├─ network.ParkRoadNetwork.load              roads
   │  ├─ rainfall.IMERGProvider.frame_grid         rain pixels around the roads
   │  ├─ rainfall ... get_decayed_precipitation    Σ rain × 0.84^days_ago per pixel
   │  └─ surface_risk.EdgeRiskEnricher.enrich      wettest pixel per edge → surface_risk
   ├─ pipeline.export_edges                        contract columns, EPSG:4326
   ├─ granule_coverage per window day              complete = all 48 half-hours
   └─ outputs.write_run → update_latest → advance_state (only if complete)
```

- A partial day (the newest IMERG day usually is) is written, but `state.json` doesn't move. So the next run redoes that day until it's complete.
- If no day is newer, the job exits 0, and the next scheduled run tries again.

**`road-risk-backfill`** ([road_risk/backfill.py](src/jobs/road_risk/backfill.py))
- It takes the same `runs.run_road_risk` over `--start`..`--end`. The default is today − 30 days to the newest IMERG day.
- It skips complete runs, so it is safe to run again.
- It moves `state.json` forward. That's how the refresh knows where to start.

**`sightings-refresh`** ([sightings/refresh.py](src/jobs/sightings/refresh.py))

```
main
├─ SightingsConfig.load(settings.toml)
├─ Sightings(config).fetch                    all years; the current and previous year always re-fetched
│  └─ SightingsService.fetch_year             GBIF pages of 300, retried
└─ run_sightings
   ├─ Geo.with_utm_xy                         lon/lat → x, y in meters
   ├─ colour_scale                            KernelShare.species_average (p0), .scale_max (vmax), 5 km layer
   └─ for each σ (10 km … 250 m): run_layer
      ├─ ShareTileBuilder.build               yields one tile at a time (256 × 256 cells, only tiles with data)
      │  ├─ LayerRecords.for_layer            for σ ≤ 2 km only records with uncertainty ≤ σ
      │  ├─ TileGrid.covering / .tiles        cell centres of the box, cut into tiles
      │  ├─ KernelShare.weighted_counts       Gaussian weights per cell: all records (n), per species (k)
      │  └─ KernelShare.beta_share / .share_range   (k + prior × p0) / (n + prior), lo/hi
      ├─ tile_payload                         the tile as JSON, null below min_records
      └─ outputs.write_file                   written right away, so memory stays small
   then outputs.write_manifest (layers + tile index) → update_latest
```

## Suggested reading order

1. [shared/outputs.py](src/jobs/shared/outputs.py): every job ends here, and it's short.
2. **Road risk:** [surface_risk.py](src/jobs/road_risk/surface_risk.py) → [rainfall.py](src/jobs/road_risk/rainfall.py) → [pipeline.py](src/jobs/road_risk/pipeline.py) → [runs.py](src/jobs/road_risk/runs.py) → [refresh.py](src/jobs/road_risk/refresh.py) / [backfill.py](src/jobs/road_risk/backfill.py).
3. **Sightings:** [models.py](src/jobs/sightings/models.py) (what the settings mean) → [kernel.py](src/jobs/sightings/kernel.py) (the maths) → [grid.py](src/jobs/sightings/grid.py) → [service.py](src/jobs/sightings/service.py) / [sightings.py](src/jobs/sightings/sightings.py) → [refresh.py](src/jobs/sightings/refresh.py).
4. **Tests:** [tests/road_risk/conftest.py](tests/road_risk/conftest.py) shows the fakes (`FakeIMERG`, fake edges) that keep the tests offline.

**Where the hand-set numbers live** (your design decisions):
- `RainfallRiskModel`: the rain-mm thresholds → 0 / 0.3 / 0.6 / 1.
- `RoadRainVulnerability.vulnerability`: the factor per road type.
- `EdgeRiskEnricher`: the base risk per road type, `risk_multiplier` and `risk_weight_minutes` (the last two are not used yet).
- `SightingsGridSettings`: kernel widths, `prior_records`, `min_records`, `confidence_records`, and the 2 km precision-filter limit.
- [settings.toml](settings.toml): the species list, the taxon (Mammalia), the box and the start year.

## Run

```bash
.venv/Scripts/python -m pip install -e "jobs[dev]"
.venv/Scripts/python -m pytest -q jobs/tests
(cd jobs && ../.venv/Scripts/ruff format src tests && ../.venv/Scripts/ruff check src tests)
.venv/Scripts/road-risk-refresh --data-dir data --cache-dir jobs/cache --ee-project <id>
```

`road-risk-backfill` and `sightings-refresh` take the same `--data-dir` / `--cache-dir` flags. Each flag falls back to an env var: `DATA_DIR`, `CACHE_DIR`, `EE_PROJECT`, `PLACE`, `LOOKBACK_DAYS`, `SETTINGS_FILE`.
