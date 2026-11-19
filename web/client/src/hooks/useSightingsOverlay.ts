import { useEffect, useRef, useState } from 'react';
import { chooseLayer, renderTile, tilesInView, type Bounds } from '@/lib/sightings';
import { getSightingsTile } from '@/services/api';
import type { SightingsLayer, SightingsRun } from '@/types/api';

// Rendered tiles by "run/species/sigma/tx_ty", shared across re-renders and toggling the layer.
const imageCache = new Map<string, Promise<string>>();

/**
 * Draws the heat map of `species` on `map`: only the tiles of the zoom's layer that are in view, each as a ground
 * overlay. Pass a null map, run or species to clear it. Returns the layer in use and how many tiles are loading.
 */
export function useSightingsOverlay(
  map: google.maps.Map | null,
  run: SightingsRun | null,
  species: string | null,
): { layer: SightingsLayer | undefined; loadingTiles: number } {
  const overlaysRef = useRef(new Map<string, google.maps.GroundOverlay>());
  // Keys of the tiles the current view needs; a tile that finishes loading after the view moved is dropped.
  const wantedRef = useRef(new Set<string>());
  const [layer, setLayer] = useState<SightingsLayer | undefined>();
  const [loadingTiles, setLoadingTiles] = useState(0);

  useEffect(() => {
    const scale = run?.species.find((s) => s.name === species);
    if (!map || !run || !species || !scale) {
      setLayer(undefined);
      return;
    }
    const overlays = overlaysRef.current;

    const update = () => {
      const zoom = map.getZoom();
      const b = map.getBounds();
      if (zoom == null || !b) return;
      const view: Bounds = {
        west: b.getSouthWest().lng(),
        south: b.getSouthWest().lat(),
        east: b.getNorthEast().lng(),
        north: b.getNorthEast().lat(),
      };
      const current = chooseLayer(run.layers, zoom, map.getCenter()?.lat() ?? -2.6);
      setLayer(current);
      const wanted = new Map(
        (current ? tilesInView(current, view) : []).map((t) => [
          `${run.date}/${species}/${current!.sigmaM}/${t.tx}_${t.ty}`,
          t,
        ]),
      );

      wantedRef.current = new Set(wanted.keys());
      overlays.forEach((overlay, key) => {
        if (!wanted.has(key)) {
          overlay.setMap(null);
          overlays.delete(key);
        }
      });

      wanted.forEach((tile, key) => {
        if (overlays.has(key) || !current) return;
        if (!imageCache.has(key)) {
          imageCache.set(
            key,
            getSightingsTile(species, current.sigmaM, tile.tx, tile.ty).then((t) => renderTile(t, scale)),
          );
        }
        setLoadingTiles((n) => n + 1);
        imageCache
          .get(key)!
          .then((url) => {
            if (overlays.has(key) || !wantedRef.current.has(key)) return;
            const [west, south, east, north] = tile.boundsWgs84;
            overlays.set(
              key,
              new google.maps.GroundOverlay(url, { west, south, east, north }, { map, clickable: false }),
            );
          })
          .catch(() => imageCache.delete(key))
          .finally(() => setLoadingTiles((n) => n - 1));
      });
    };

    const listener = map.addListener('idle', update);
    update();
    return () => {
      // A map whose key Google rejected can hand back no listener, so don't assume one.
      listener?.remove();
      wantedRef.current = new Set();
      overlays.forEach((o) => o.setMap(null));
      overlays.clear();
    };
  }, [map, run, species]);

  return { layer, loadingTiles };
}
