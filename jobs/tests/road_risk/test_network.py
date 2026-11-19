from jobs.road_risk.network import ParkRoadNetwork


def test_vehicle_road_filter():
    network = ParkRoadNetwork("anywhere")
    assert network._is_vehicle_road("track")
    assert not network._is_vehicle_road("footway")
    assert not network._is_vehicle_road(["track", "path"])
