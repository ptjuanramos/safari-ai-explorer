from dataclasses import dataclass, field

import geopandas as gpd
import numpy as np
import pandas as pd

from jobs.shared.crs import UTM


@dataclass
class RainfallRiskModel:
    """
    Model for converting accumulated rainfall into a normalized rainfall risk.
    The model uses configurable rainfall thresholds to map accumulated, to a risk score between 0 and 1.

    Default thresholds:
        - >= 30 mm -> 1.0
        - >= 15 mm -> 0.6
        - >= 5 mm  -> 0.3
        - >= 0 mm  -> 0.0


    TODO:
        - Improve the rainfall-risk model to account for additional factors such
        as soil type, drainage capacity, terrain, rainfall duration, and
        antecedent moisture conditions.
        - Thresholds should also account for the rainfall accumulation period.
        The same accumulated rainfall can represent significantly different
        risk levels depending on whether it occurred over one day or several
        days.
    """

    thresholds: tuple[tuple[float, float], ...] = (
        (30.0, 1.0),
        (15.0, 0.6),
        (5.0, 0.3),
        (0.0, 0.0),
    )

    def risk_for(self, accumulated_mm: float) -> float:
        """
        :param accumulated_mm:
        :return:  Risk score between 0.0 and 1.0 corresponding to the highest rainfall threshold.
        """
        if pd.isna(accumulated_mm):
            return 0.0
        for min_mm, risk in self.thresholds:
            if accumulated_mm >= min_mm:
                return risk
        return 0.0


@dataclass
class RoadRainVulnerability:
    """
    How much a given road type's passability is
        affected by the same amount of rain. A motorway barely cares, a track does.
    The factor is then used to multiply with the rainfall risk model.
    """

    vulnerability: dict = field(
        default_factory=lambda: {
            "motorway": 0.1,
            "trunk": 0.1,
            "primary": 0.1,
            "secondary": 0.3,
            "tertiary": 0.4,
            "residential": 0.4,
            "service": 0.5,
            "unclassified": 0.7,
            "track": 1.0,
        }
    )

    default: float = 0.5

    def factor_for(self, road_type) -> float:
        """
        :param road_type: The road type
        :return: Probability factor of the road_type has a vulnerability with rain.
        """

        if isinstance(road_type, list):
            road_type = road_type[0]
        return self.vulnerability.get(str(road_type), self.default)


@dataclass
class EdgeRiskEnricher:
    """
    Spatially joins edges to rainfall pixels, then combines
        the existing road-type base risk with the rainfall risk, weighted by how
        rain-vulnerable that road type is.
    """

    HIGHWAY_SURFACE_RISK = {
        "motorway": 0.0,
        "trunk": 0.0,
        "primary": 0.0,
        "secondary": 0.02,
        "tertiary": 0.05,
        "residential": 0.05,
        "service": 0.07,
        "unclassified": 0.1,
        "track": 0.15,
    }

    risk_multiplier: float = 2.5
    default_base_risk: float = 0.5
    rain_risk_model: RainfallRiskModel = field(default_factory=RainfallRiskModel)
    vulnerability: RoadRainVulnerability = field(default_factory=RoadRainVulnerability)
    risk_weight_minutes: float = 60.0  # minutes penalty for surface_risk = 1.0

    def _base_risk_for(self, road_type) -> float:
        if isinstance(road_type, list):
            road_type = road_type[0]
        return self.HIGHWAY_SURFACE_RISK.get(str(road_type), self.default_base_risk)

    def enrich(self, edges: gpd.GeoDataFrame, rain_pixels: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
        """
        Calculate rainfall-related surface risk for road edges with extra attributes:

        - `rain_risk`
        - `base_road_risk`
        - `road_vulnerability`
        - `surface_risk`: `min(1, base_road_risk + road_vulnerability * rain_risk)`

        :param edges: OSMnx road edges.
        :type edges: GeoDataFrame
        :param rain_pixels: Extracted rainfall pixels containing rainfall data.
        :type rain_pixels: GeoDataFrame

        :return: Complementary edges dataframe with extra attributes
        :rtype: GeoDataFrame
        """
        joined = gpd.sjoin(
            edges.reset_index().to_crs(UTM),
            rain_pixels[["pixel_id", "rain_mm", "geometry"]].to_crs(UTM),
            how="left",
            predicate="intersects",
        )

        # TODO: need review - an edge can straddle more than one pixel; take the worst (highest) rainfall it touches
        worst_rain = joined.groupby(["u", "v", "key"])["rain_mm"].max()

        edges = edges.copy()
        edges["rain_mm"] = worst_rain.reindex(edges.index).values
        edges["rain_risk"] = edges["rain_mm"].apply(self.rain_risk_model.risk_for)
        edges["base_road_risk"] = edges["road_type"].apply(self._base_risk_for)
        edges["road_vulnerability"] = edges["road_type"].apply(self.vulnerability.factor_for)

        edges["surface_risk"] = np.minimum(
            1.0,
            edges["base_road_risk"]
            + edges["road_vulnerability"] * edges["rain_risk"],  # TODO: calcualtion may needs to be fixed.
        )

        return edges
