from pathlib import Path

import pytest

from jobs.road_risk.config import RoadRiskConfig

SETTINGS = Path(__file__).parents[2] / "settings.toml"


def test_settings_list_the_northern_parks():
    places = RoadRiskConfig.load(SETTINGS).places
    assert "Tarangire National Park, Tanzania" in places
    assert "Serengeti National Park, Tanzania" in places
    assert len(places) == len(set(places)) == 5


def test_place_flag_runs_only_that_place_without_reading_settings(tmp_path):
    missing = tmp_path / "missing.toml"
    assert RoadRiskConfig.places_to_run(missing, "Somewhere, Tanzania") == ("Somewhere, Tanzania",)
    assert RoadRiskConfig.places_to_run(SETTINGS, None) == RoadRiskConfig.load(SETTINGS).places


def test_an_empty_place_list_is_an_error(tmp_path):
    settings = tmp_path / "settings.toml"
    settings.write_text("[road_risk]\nplaces = []\n")
    with pytest.raises(ValueError, match="empty"):
        RoadRiskConfig.load(settings)
