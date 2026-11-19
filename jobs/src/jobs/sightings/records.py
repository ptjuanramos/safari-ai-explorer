from typing import ClassVar

import pandas as pd

from jobs.shared.geo import Geo
from jobs.sightings.models import SightingsGridSettings


class LayerRecords:
    """Selects the records that go into one layer (one kernel width)."""

    UNCERTAINTY: ClassVar[str] = "coordinateUncertaintyInMeters"

    @classmethod
    def precise(cls, records: pd.DataFrame, max_uncertainty_m: float) -> pd.DataFrame:
        """Keeps records whose location is known within `max_uncertainty_m`. Unknown uncertainty counts as precise."""
        if cls.UNCERTAINTY not in records:
            return records
        uncertainty = records[cls.UNCERTAINTY]
        return records[uncertainty.isna() | (uncertainty <= max_uncertainty_m)]

    @classmethod
    def for_layer(cls, records: pd.DataFrame, sigma_m: float, settings: SightingsGridSettings) -> pd.DataFrame:
        """Records with UTM `x`, `y`; for fine layers (sigma <= 2 km) only those precise enough for sigma."""
        located = Geo.with_utm_xy(records)
        max_uncertainty_m = settings.max_uncertainty_m(sigma_m)
        return located if max_uncertainty_m is None else cls.precise(located, max_uncertainty_m)
