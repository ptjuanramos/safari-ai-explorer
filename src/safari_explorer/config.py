from dataclasses import dataclass

SPECIES = {
    "buffalo": 2441034,
    "elephant": 2435350,
    "lion": 5219404,
    "cheetah": 2435270,
    "leopard": 5219436,
}


@dataclass(frozen=True)
class Config:
    bounds: tuple = (34.0, -3.6, 36.0, -1.6)
    start_year: int = 2017
    end_year: int = 2025
    cache_dir: str | None = None
