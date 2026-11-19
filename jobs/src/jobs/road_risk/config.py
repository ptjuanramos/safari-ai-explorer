import tomllib
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class RoadRiskConfig:
    """
    Which places the road-risk jobs cover, loaded from the `[road_risk]` section of `settings.toml`.

    Attributes:
        places: OSM place names, one road network and one run folder (`road-risk/<slug>/`) each.
    """

    places: tuple[str, ...]

    @classmethod
    def load(cls, path: str | Path) -> "RoadRiskConfig":
        with open(path, "rb") as f:
            section = tomllib.load(f)["road_risk"]
        places = tuple(section["places"])
        if not places:
            raise ValueError(f"{path}: [road_risk] places is empty")
        return cls(places=places)

    @classmethod
    def places_to_run(cls, settings: str | Path, only: str | None) -> tuple[str, ...]:
        """Just `only` when it's given (--place / PLACE); otherwise every place in settings, which is only read then."""
        return (only,) if only else cls.load(settings).places
