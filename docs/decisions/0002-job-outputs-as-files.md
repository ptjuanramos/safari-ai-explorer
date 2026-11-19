# ADR-0002: Job outputs are versioned files on a shared volume

**Date:** 15.11.2026  
**Status:** Accepted. Amended by ADR-0004: the API can also read the same files from a blob container.

## Context

The project now has two Python jobs (`sightings-refresh`, `road-risk-refresh`, plus a manual `road-risk-backfill`) and a .NET API that serves their results to a React UI. They run as separate Docker containers, so the jobs need a place to hand data to the API.

The data is small (a few MB per run), written once a day at most, and read whole. Running a database for it would add a container, migrations and credentials without any query the API needs today.

## Decision

- Each job writes a **run** into `DATA_DIR/<job>/…/<run-id>/`, where the run id is an ISO date:
  - the data files first;
  - then `manifest.json`, which describes the run;
  - then an atomic replace of `latest.json` in the job folder (temp file + rename).
- A run folder without `manifest.json` is incomplete and is ignored by the API.
- A run id is the **data date** (for road risk, the last rain day), not the day the job ran. The jobs' progress lives in a small `state.json` per job folder (road risk: `complete_through`), which maps to one database row later.
- The API mounts the volume **read-only**. (The one exception is its own `user-paths/` folder: see ADR-0003.)
- The formats are defined in [data-contract.md](../data-contract.md).
- On the .NET side, the files are read only by the adapters in `SafariExplorer.Infrastructure` that implement the ports `IRoadRiskRepository` and `ISightingsRepository` (since ADR-0004: `JobData/` over a folder or a blob container).
  - **The repositories return domain models**, never files or streams. Use cases and endpoints don't know where the data lives.

## Consequences

- There's no database to run, and every run can be inspected as plain JSON/GeoJSON.
- Past runs stay on disk, so history (backfills, `/api/road-risk/dates`) comes for free. Disk use grows with the number of runs, and nothing prunes them yet.
- **Exit path:** to move to a database (for example PostGIS), add an adapter next to `FileSystem/` that implements the same ports, and point the jobs' writer at it. Only `Program.cs` changes on the API side.
- Concurrent writers to the same run aren't coordinated. Today one scheduled job per run id is assumed.
