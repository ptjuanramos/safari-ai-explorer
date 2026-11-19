import numpy as np
import pandas as pd

from jobs.sightings.models import SightingsGridSettings
from jobs.sightings.records import LayerRecords


def test_precise_keeps_unknown_and_small_uncertainty():
    records = pd.DataFrame({"coordinateUncertaintyInMeters": [np.nan, 30.0, 250.0, 5_000.0, 27_000.0]})
    assert LayerRecords.precise(records, 250)["coordinateUncertaintyInMeters"].tolist()[1:] == [30.0, 250.0]
    assert len(LayerRecords.precise(records, 10_000)) == 4
    assert len(LayerRecords.precise(records.drop(columns="coordinateUncertaintyInMeters"), 250)) == 5


def test_only_layers_up_to_2_km_are_filtered():
    records = pd.DataFrame(
        {
            "decimalLongitude": [35.0] * 10,
            "decimalLatitude": [-2.5] * 10,
            "speciesKey": [1] * 10,
            "coordinateUncertaintyInMeters": [31_000.0] * 5 + [10.0] * 5,
        }
    )
    settings = SightingsGridSettings()
    assert [settings.max_uncertainty_m(s) for s in settings.sigmas_m] == [None, None, 2_000, 1_000, 500, 250]
    assert len(LayerRecords.for_layer(records, 5_000, settings)) == 10
    assert len(LayerRecords.for_layer(records, 2_000, settings)) == 5
    assert {"x", "y"} <= set(LayerRecords.for_layer(records, 2_000, settings).columns)
