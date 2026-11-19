FROM python:3.13-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    DATA_DIR=/data \
    CACHE_DIR=/cache \
    SETTINGS_FILE=/app/settings.toml

WORKDIR /app
COPY pyproject.toml settings.toml ./
COPY src/ src/
RUN pip install --no-cache-dir .

ENTRYPOINT ["road-risk-refresh"]
