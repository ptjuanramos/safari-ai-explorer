# Safari AI Explorer

1. **Sightings heat map:** where visitors see buffalo, elephant, lion, cheetah and leopard. Built from GBIF sightings only.
2. **Road surface risk:** how likely each park road is to be muddy or hard to pass, given the rain of the last days. Built from OpenStreetMap roads and Earth Engine rainfall.

Both started in the exploration repo `../safari-ai-explorer-notebook`.

## Architecture decisions

ADRs live in `docs/decisions/`. Read them before making any spatial choice.

| ADR | Summary |
|-----|---------|
| [0001](docs/decisions/0001-metric-coordinate-system.md) | Always use EPSG:32736 (UTM zone 36S, meters) for spatial ops, never degrees |


## Sightings refresh job

- **Effort-adjusted share:** (weighted records of the species) / (weighted records of all mammals) around each pixel. The weights are a Gaussian kernel in UTM meters.
  - Gaussian Kernel: A bell-shaped mathematical function that measures similarity or distance between data points
- **Bias:** this is where visitors see each animal, with most of the "more tourists" effect removed, not a population survey.

## How the road surface risk works

1. **Roads:** `ParkRoadNetwork` loads the OSMnx graph for the place (`network_type="all"`), adds speeds and travel times, and drops `footway`/`path`.
2. **Rain grid:** `frame_grid(edges.total_bounds)` builds pixel boxes aligned to the dataset resolution, in EPSG:4326, because the boxes must match the dataset's native degree pixels.
3. **Rain index:** `get_decayed_precipitation(grid, date, lookback_days)` sums daily rain × k^days_ago with k = 0.84 + 0.012 × (t_clt − t_avg). The temperatures are fixed at 20 °C, so k = 0.84.
   - It raises an error if Earth Engine returns a different number of days than asked, or if `lookback_days < 1`.
4. **Joining rain to roads:** `EdgeRiskEnricher` joins edges to pixels in UTM (ADR-0001), and each edge takes the wettest pixel it touches.
5. **Risk score:** `surface_risk = min(1, base_road_risk + road_vulnerability × rain_risk)`.



**Open issues (from the user's TODOs and review):**
- No temperature term yet (`t_clt = t_avg = 20`).
- The `RainfallRiskModel` thresholds were meant for accumulated mm, but they're applied to the decayed index, and they don't depend on the lookback length.
- Taking the max over the pixels an edge straddles needs review.
- Access restrictions (`access`, `motor_vehicle`, private roads) aren't filtered.
- `EdgeRiskEnricher.risk_multiplier` and `risk_weight_minutes` (minutes of penalty at risk 1.0, probably meant for routing) are not used yet.
- The weights are hand-set and not validated against real road-condition reports.
- `scale_m` (5566 / 11132) is the degree pixel size written in meters, as noted in ADR-0001.
- The place is Tarangire NP, while the heat map box is the Serengeti.



## Conventions

- **Units:** meters and UTM.
- **Comments:**
  - Don't add generated comments or docstrings (the user had those removed).
  - **Keep the user's own docstrings, comments and TODOs**, which are in the road risk classes.
- **Tests:** every new calculation gets a pytest test that needs no network.
- **Honesty:** GBIF records are visitor-biased and the road risk is unvalidated. Say what a map can and can't show.

## Data notes

- **GBIF:** deep paging can time out or return 503. `get_page` retries with growing waits, and fetching one year at a time keeps offsets small.
  - The Serengeti box includes the Kenyan side (Masai Mara), because records are filtered by coordinates, not country.
- **CHIRPS:** the final product lags by a few weeks, so use IMERG for recent dates.
- **IMERG:** checks the granule count for every day with a separate `getInfo` call, which is slow for long lookbacks.
- **Next ideas:**
  - seasonality for the heat map (add `month` to `COLUMNS`)
  - a GBIF publisher breakdown
  - wiring `risk_weight_minutes` into routing
  - validating road risk against ranger or guide reports
