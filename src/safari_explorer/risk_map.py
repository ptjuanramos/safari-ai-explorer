from datetime import date

import folium
import geopandas as gpd

from .rainfall import CHIRPSProvider, RainGridProvider
from .road_network import ParkRoadNetwork
from .surface_risk import EdgeRiskEnricher

SURFACE_RISK_COLORS = {
    (0.0, 0.25): "#2ecc71",
    (0.25, 0.5): "#f39c12",
    (0.5, 0.75): "#e74c3c",
    (0.75, 1.01): "#7b241c",
}


def get_risk_color(surface_risk: float) -> str:
    for (low, high), color in SURFACE_RISK_COLORS.items():
        if low <= surface_risk < high:
            return color
    return "#7b241c"


def surface_risk_map(nodes: gpd.GeoDataFrame, edges_enriched: gpd.GeoDataFrame, rain_label: str = "rain_mm") -> folium.Map:
    center_lat, center_lon = nodes["y"].mean(), nodes["x"].mean()
    m = folium.Map(location=[center_lat, center_lon], zoom_start=10)

    for _, row in edges_enriched.iterrows():
        coords = [(lat, lon) for lon, lat in row["geometry"].coords]
        folium.PolyLine(
            locations=coords,
            color=get_risk_color(row["surface_risk"]),
            weight=3,
            opacity=0.8,
            popup=(
                f"highway: {row['road_type']}<br>"
                f"travel_time: {row['travel_time_m']:.1f} min<br>"
                f"{rain_label}: {row['rain_mm']:.1f}<br>"
                f"surface_risk: {row['surface_risk']:.2f}"
            ),
        ).add_to(m)

    return m


def create_risk_map(place_name: str, day: date, lookback_days: int,
                    rain_provider: RainGridProvider | None = None) -> folium.Map:
    rain_provider = rain_provider or CHIRPSProvider()
    nodes, edges = ParkRoadNetwork(place_name).load()

    pixel_grid = rain_provider.frame_grid(edges.total_bounds)
    rain_pixels, _ = rain_provider.get_decayed_precipitation(
        pixel_grid,
        date=day.strftime("%Y-%m-%d"),
        lookback_days=lookback_days,
    )

    edges_enriched = EdgeRiskEnricher().enrich(edges, rain_pixels)
    label = f"rain index (mm, decayed over {lookback_days} days, {type(rain_provider).__name__})"
    return surface_risk_map(nodes, edges_enriched, rain_label=label)
