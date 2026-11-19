# ADR-0003: The API saves user paths as files in its own folder of the data volume

**Date:** 18.11.2026  
**Status:** Accepted. Amended by ADR-0004: with `Storage:Kind` = `Azure`, paths are table rows instead of files.

## Context

The trip's drives ("paths") used to live only in the browser, starting from a hard-coded sample trip, and were lost on reload. They now need to be saved.

This is the first data the **API** writes. ADR-0002 is about job outputs: written once by a job, versioned by date, and read-only for the API. User paths are different: they're small, mutable (added and deleted one at a time), and have no data date.

## Decision

- The API saves each path as one JSON file: `DATA_DIR/user-paths/<id>.json`.
  - The id is 32 lowercase hex digits, made by the API. The adapter only accepts slugs, so an id can't leave the folder.
  - Each write goes to a temp file first and is then renamed, so a reader never sees half a file.
  - There is no manifest and no `latest.json`: a path either has a file or doesn't exist.
- The format is in [data-contract.md](../data-contract.md#user-paths). It has a `version` field (1 today).
- On the .NET side, the port is `IUserPathRepository`, and `FileSystem/FileUserPathRepository` implements it. The use cases are `ListUserPaths`, `AddUserPath` (at most 200 paths, since there is no login) and `DeleteUserPath`.
- **Docker:** the web container still mounts the volume **read-only** at `/data`. Only the `user-paths` sub-folder is mounted read-write (a Compose volume `subpath`, Docker Engine 26+), so the API can't change the jobs' runs.
- The sample trip is gone: the itinerary is whatever is saved.

## Consequences

- Paths survive reloads and container restarts, and each one can be read as plain JSON.
- The `user-paths` folder must exist in the volume before `docker compose up web`, because a subpath mount fails otherwise:
  `docker run --rm -v safari-data:/data alpine mkdir -p /data/user-paths`. Locally (`DATA_DIR=data`), the API creates it.
- There is one trip, shared by everyone who can reach the API: no users, no auth. The 200-path cap only limits disk use.
- Two browsers deleting or adding at the same moment aren't coordinated, but each write touches only its own file.
- **Exit path:** as in ADR-0002, a database adapter can implement `IUserPathRepository` next to `FileSystem/`, with one line changed in DI. A `trips` table plus a `user_paths` table would be the natural next step once there are users.
