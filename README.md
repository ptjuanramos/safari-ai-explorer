# Safari AI Explorer

Data for a safari app in northern Tanzania: where visitors see buffalo, elephant, lion, cheetah and leopard (from GBIF sightings), and how likely each park road is to be muddy after the last days of rain (from OpenStreetMap and Earth Engine). Python jobs produce the data, a .NET API serves it, and a React app shows it.

**Units:** meters and UTM (EPSG:32736) for every distance, buffer, kernel or spatial join; lon/lat only for API input, the native rain pixel grid and display ([ADR-0001](docs/decisions/0001-metric-coordinate-system.md)).

## Open issues

- [ ] No temperature term yet (`t_clt = t_avg = 20`).
- [ ] The `RainfallRiskModel` thresholds were meant for accumulated mm, but they're applied to the decayed index, and they don't depend on the lookback length.
- [ ] Taking the max over the pixels an edge straddles needs review.
- [ ] Access restrictions (`access`, `motor_vehicle`, private roads) aren't filtered.
- [ ] `EdgeRiskEnricher.risk_multiplier` and `risk_weight_minutes` (minutes of penalty at risk 1.0, probably meant for routing) are not used yet.
- [ ] The weights are hand-set and not validated against real road-condition reports.
- [ ] `scale_m` (5566 / 11132) is the degree pixel size written in meters, as noted in ADR-0001.
- [x] ~~The place is Tarangire NP, while the heat map box is the Serengeti.~~ Road risk now covers five northern parks and the heat map all of Tanzania.
- [ ] The Tanzania-wide sightings box has about 7× the records of the Serengeti box: the 2026-11-18 run (139,573 records, 1,475 tiles) is 1.7 GB, against 196 MB before.
- [ ] OSM's Arusha NP polygon (about 116 km²) leaves out most of the 550 km² park, including Mount Meru.
- [ ] Most buffalo, elephant, lion, cheetah and leopard records have hidden locations (iNaturalist, about 31 km uncertainty), so the 2 km and finer sightings layers are almost empty for them. Spreading each hidden record over its 0.2° cell could replace dropping it.
- [ ] A sightings run is about 196 MB, mostly empty cells in the 500 m and 250 m tiles. It needs a sparser format before running weekly.
- [ ] The jobs still write folders only (ADR-0004), so a new run reaches the storage account only when it's uploaded by hand. Next step: a `JobStore` behind `shared/outputs.py` that writes blobs, with ETag conditions on `latest.json` and `state.json`.
- [ ] The sightings tiles are stored as plain JSON in the storage account. Gzipping them would cut size and transfer about 5–10× once runs are uploaded often.