# ADR-0004: Job data in Blob Storage, saved paths in Table Storage

**Date:** 19.11.2026  
**Status:** Accepted. Amends ADR-0002 (where the API reads the job files) and ADR-0003 (where saved paths are kept).

## Context

The job outputs (about 3 GB: road risk for five parks, and a 1.7 GB sightings run) and the user's saved paths live on a Docker volume. The data is moving to an Azure Storage account, so the API can run without that volume. The jobs won't run for a while, so the job files are uploaded once rather than written to the account by the jobs.

## Decision

- **Job files go into one blob container, `job-data`, under the same keys as the folders**: `road-risk/<place>/<date>/manifest.json`, `sightings/latest.json`, and so on. [data-contract.md](../data-contract.md) is unchanged, and so are the rules of ADR-0002 (a run without a manifest doesn't exist; `latest.json` names the newest run).
- **Saved paths go into one table, `userpaths`**, one row per path. The partition key is `trip` (one trip for now) and the row key is the path id. The stops are a JSON string, because a table column can't hold a list. The schema is in the data contract.
- **The API reads job files through a small store interface**, `IJobStore` (`ReadJsonAsync`, `ExistsAsync`, `ListKeysAsync`), with two implementations:
  - `FileSystem/LocalJobStore` for a folder;
  - `AzureStorage/BlobJobStore` for a container.
  - `JobData/RoadRiskRepository` and `JobData/SightingsRepository` parse the files from either store, so the parsing exists once. Keys are checked to have no empty, `.` or `..` segments, as well as the slug checks.
- **`Storage:Kind` picks the adapters:** `Files` (the default, as before) or `Azure`. The ports, use cases, endpoints and UI don't change.
- **Sign-in:**
  - With a connection string when one is set (`AZURE_STORAGE_CONNECTION_STRING`: Azurite, quick tests, Docker).
  - Otherwise with the account URL (`STORAGE_ACCOUNT_URL`, the blob endpoint; the table endpoint is derived from it) and `DefaultAzureCredential`: `az login` locally, a managed identity in Azure. No account key is needed in Azure.
- **Roles:**
  - The API needs **Storage Blob Data Reader** on `job-data`, so it still can't change the jobs' runs (ADR-0002).
  - It needs **Storage Table Data Contributor** on `userpaths`.
  - Whoever uploads runs needs **Storage Blob Data Contributor**.
- **Cache:** the sightings repository keeps parsed manifests for 10 minutes, because every tile request needs the manifest and it no longer comes from a local disk. `latest.json` isn't cached.
- **Import:** the paths saved as files are copied once with `dotnet run --project web/src/SafariExplorer.Api -- import-user-paths [data folder]`. Running it again replaces the same rows.

## Consequences

- The API no longer needs the data volume in `Azure` mode. In `Files` mode nothing changes, including Docker's read-only volume and the `user-paths` subpath.
- **Uploading new runs is manual while the jobs only write folders** (`az storage blob upload-batch`). Writing straight to the container from the jobs is the next step: a `JobStore` behind `shared/outputs.py`, with ETag conditions on the "only forward" `latest.json` and `state.json`.
- **Listing a park's runs is one blob listing**, not one request per date, so `GetRouteRisk` (which lists every park on each request) stays cheap.
- **Tests:**
  - The repositories are tested on `LocalJobStore` as before.
  - The Azure adapters are tested against Azurite (`docker compose --profile azurite up -d azurite`), and those tests are skipped when it isn't running.
- **Writes:** a table row is written in one call, so the temp-file-and-rename step of ADR-0003 isn't needed. Two browsers saving the same id at once: the last write wins, as with files.
- **Cost:** about 3 GB in hot storage plus small reads. The sightings tiles are plain JSON; gzipping them would cut size and transfer about 5–10× once runs are uploaded often.
