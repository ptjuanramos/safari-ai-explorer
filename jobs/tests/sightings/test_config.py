from jobs.sightings.config import SightingsConfig


def test_settings_file_holds_the_five_species(config):
    assert list(config.species) == ["buffalo", "elephant", "lion", "cheetah", "leopard"]
    assert config.species["lion"] == 5219404
    assert config.taxon_key == 359
    assert config.bounds == (29.3, -11.8, 40.5, -0.95)
    assert config.start_year == 2017 and config.end_year is None and config.cache_dir is None


def test_load_applies_overrides(tmp_path):
    settings = tmp_path / "settings.toml"
    settings.write_text(
        "[sightings]\ntaxon_key = 1\nbounds = [0, 0, 1, 1]\nstart_year = 2020\n[sightings.species]\nzebra = 7\n"
    )
    config = SightingsConfig.load(settings, end_year=2026, cache_dir="c")
    assert config == SightingsConfig({"zebra": 7}, 1, (0, 0, 1, 1), 2020, 2026, "c")
