from pathlib import Path

import pytest

from jobs.sightings.config import SightingsConfig

SETTINGS = Path(__file__).parents[2] / "settings.toml"


@pytest.fixture(scope="session")
def config() -> SightingsConfig:
    return SightingsConfig.load(SETTINGS)


@pytest.fixture(scope="session")
def species(config) -> dict[str, int]:
    return config.species
