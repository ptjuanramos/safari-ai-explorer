# Conventions

## Settings

- Application settings (species, taxon, box, years) live in `jobs/settings.toml`, loaded into `SightingsConfig`. Tunables that belong to the code live in dataclasses (`SightingsGridSettings`, `SightingsService`, the providers, the risk models) on the Python side, and in options classes (`DataOptions`, `RoadRiskOptions`) on the .NET side.
- Species are looked up in `config.species`, never hard-coded.

## Structure

- The jobs live in one package, `jobs`, with one subpackage per feature (`sightings`, `road_risk`) plus `shared`. A job's entrypoint sits in its feature (`refresh.py`, `backfill.py`).
- Use absolute imports (`from jobs.shared.outputs import ...`).
- Group behaviour in classes. A method that doesn't use `self` is a `@staticmethod`; one that only needs other class members is a `@classmethod`. Helpers used only inside a class start with `_`. Class-level constants are `ClassVar`s in `UPPER_CASE`.
- Calculations live in utility classes (`KernelShare`, `Geo`), separate from the code that orchestrates them (`ShareTileBuilder`, the jobs).
- Talk to external APIs through a service class (`SightingsService`), so the source can be swapped without touching the rest.
- Formatting is `ruff format` (line length 120) and `ruff check`; run both before handing code back.
- The Python code is map-agnostic: no map or colour code. The jobs export data, and the UI draws it.
- The user is learning, so explain the why in docs and reviews.

## Comments

- Document every field of the dataclasses (settings, models, services) in an `Attributes:` section, and give utility methods a one-line docstring that says what they return and why. Don't add comments that narrate the code.
- **Keep the user's own docstrings, comments and TODOs**, which are in the road risk classes.

## Tests

- Every new calculation gets a test that needs no network: pytest in `jobs/tests` (mirroring the package), xUnit in `web/tests`.
- GBIF, Earth Engine and OSMnx are monkeypatched or faked. The .NET tests write their own data folder.

## Data files

- Jobs write data files first and `manifest.json` last, then move `latest.json`. A run without a manifest doesn't exist.
- The formats are in [data-contract.md](data-contract.md). Change the Python writer and the .NET reader together.
