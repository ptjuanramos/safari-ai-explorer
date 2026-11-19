import tomllib
from dataclasses import dataclass, replace
from pathlib import Path


@dataclass(frozen=True)
class SightingsConfig:
    """
    What the sightings job fetches, loaded from the `[sightings]` section of `settings.toml`.

    Attributes:
        species: Species on the map, name -> GBIF speciesKey.
        taxon_key: GBIF classKey of the group whose records form the visitor-effort baseline (359 = Mammalia).
        bounds: Box as (west, south, east, north) in degrees.
        start_year: First year of records.
        end_year: Last year of records. The job sets it to the current year.
        cache_dir: Folder for the yearly parquet cache. `None` means no cache.
    """

    species: dict[str, int]
    taxon_key: int
    bounds: tuple[float, float, float, float]
    start_year: int
    end_year: int | None = None
    cache_dir: str | None = None

    @classmethod
    def load(cls, path: str | Path, **overrides) -> "SightingsConfig":
        with open(path, "rb") as f:
            section = tomllib.load(f)["sightings"]

        config = cls(
            species=dict(section["species"]),
            taxon_key=int(section["taxon_key"]),
            bounds=tuple(section["bounds"]),
            start_year=int(section["start_year"]),
        )
        return replace(config, **overrides)
