import { useEffect, useRef, useState } from 'react';
import type { Segment, PredictionMap } from '@/types/trip';
import type { RoadNetwork } from '@/types/api';
import { useSightingsOverlay } from '@/hooks/useSightingsOverlay';
import { useSightingsRun } from '@/hooks/useSightingsRun';
import { loadGoogleMaps, onGoogleMapsAuthFailure } from '@/lib/googleMapsLoader';
import { riskHex } from '@/lib/roadRisk';
import { getRoadNetwork } from '@/services/api';
import { SegmentDetailCard } from './SegmentDetailCard';
import { LayerToggles, type LayerState } from './LayerToggles';
import { SightingsPanel } from './SightingsPanel';

interface Props {
  /** Segments with their driving paths already resolved (the same paths the road risk was computed on). */
  segments: Segment[];
  predictions: PredictionMap;
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  /** Each park's road-risk run in use (its place slug and rain day), for the park-roads layer. */
  networkRuns: { place: string; date: string }[];
}

const networkCache = new Map<string, Promise<RoadNetwork>>();

// Slate grey: a route with no road-risk score (it doesn't run on any mapped park road).
const NO_DATA_HEX = '#475569';

export function MapPanel({ segments, predictions, selectedId, onSelect, networkRuns }: Props) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const polylinesRef = useRef<google.maps.Polyline[]>([]);
  const waypointMarkersRef = useRef<google.maps.Marker[]>([]);

  const [ready, setReady] = useState(false);
  // The same map as mapRef, as state, so hooks re-run once it exists.
  const [mapInstance, setMapInstance] = useState<google.maps.Map | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [layers, setLayers] = useState<LayerState>({ network: false, sightings: false });
  const [detailOpen, setDetailOpen] = useState(true);
  const [species, setSpecies] = useState<string | null>(null);

  // The sightings heat map, on the same map as the routes so both can be read together.
  const sightings = useSightingsRun(layers.sightings);
  const selectedSpecies = species ?? sightings.run?.species[0]?.name ?? null;
  const { layer: sightingsLayer, loadingTiles } = useSightingsOverlay(
    layers.sightings ? mapInstance : null,
    sightings.run,
    selectedSpecies,
  );

  const effectiveSegments = segments;

  const toggle = (k: keyof LayerState) =>
    setLayers((l) => ({ ...l, [k]: !l[k] }));

  useEffect(() => onGoogleMapsAuthFailure(setError), []);

  // Init map once
  useEffect(() => {
    let cancelled = false;
    loadGoogleMaps()
      .then((google) => {
        if (cancelled || !containerRef.current) return;
        const map = new google.maps.Map(containerRef.current, {
          center: { lat: -2.8, lng: 35.5 },
          zoom: 8,
          disableDefaultUI: false,
          zoomControl: true,
          zoomControlOptions: {
            position: google.maps.ControlPosition.RIGHT_CENTER,
          },
          mapTypeControl: true,
          mapTypeControlOptions: window.matchMedia('(min-width: 640px)').matches
            ? undefined
            : { style: google.maps.MapTypeControlStyle.DROPDOWN_MENU, position: google.maps.ControlPosition.LEFT_BOTTOM },
          mapTypeId: 'terrain',
          streetViewControl: false,
          fullscreenControl: false,
          clickableIcons: false,
          scrollwheel: true,
          gestureHandling: 'greedy',
        });
        map.addListener('click', () => onSelect(null));
        mapRef.current = map;
        setMapInstance(map);
        setReady(true);
      })
      .catch((e: Error) => setError(e.message));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Render route polylines + waypoint markers
  useEffect(() => {
    const map = mapRef.current;
    if (!ready || !map || !window.google) return;
    const g = window.google;

    polylinesRef.current.forEach((p) => p.setMap(null));
    polylinesRef.current = [];
    waypointMarkersRef.current.forEach((m) => m.setMap(null));
    waypointMarkersRef.current = [];

    const bounds = new g.maps.LatLngBounds();

    effectiveSegments.forEach((s) => {
      const pred = predictions[s.id];
      const color = pred ? riskHex(pred.surfaceRisk) : NO_DATA_HEX;
      const isSelected = s.id === selectedId;

      const baseLine = new g.maps.Polyline({
        path: s.path,
        map,
        strokeColor: color,
        strokeOpacity: 0.95,
        strokeWeight: isSelected ? 8 : 5,
        zIndex: isSelected ? 10 : 2,
        clickable: true,
      });
      baseLine.addListener('click', () => {
        onSelect(s.id);
        setDetailOpen(true);
      });
      polylinesRef.current.push(baseLine);

      s.path.forEach((pt) => bounds.extend(pt));
    });

    // Waypoint markers (origin of first + destination of each)
    const wpSet = new Map<string, { pos: google.maps.LatLngLiteral; name: string }>();
    effectiveSegments.forEach((s, i) => {
      if (i === 0) {
        const p = s.path[0];
        wpSet.set(`${p.lat},${p.lng}`, { pos: p, name: s.origin });
      }
      const last = s.path[s.path.length - 1];
      wpSet.set(`${last.lat},${last.lng}`, { pos: last, name: s.destination });
    });
    wpSet.forEach(({ pos, name }) => {
      const m = new g.maps.Marker({
        position: pos,
        map,
        title: name,
        label: { text: name, fontSize: '11px', fontWeight: '600', className: 'gm-wp-label' },
        icon: {
          path: g.maps.SymbolPath.CIRCLE,
          scale: 6,
          fillColor: '#ffffff',
          fillOpacity: 1,
          strokeColor: '#0f172a',
          strokeWeight: 2,
        },
      });
      waypointMarkersRef.current.push(m);
    });

    // Initial fit only when nothing is selected; selected-segment fit lives in a separate effect.
    if (!selectedId && !bounds.isEmpty()) {
      map.fitBounds(bounds, 60);
    }
  }, [ready, effectiveSegments, predictions, selectedId, onSelect]);

  // Focus on selected segment
  useEffect(() => {
    const map = mapRef.current;
    if (!ready || !map || !window.google) return;
    const g = window.google;
    if (!selectedId) return;
    const seg = effectiveSegments.find((s) => s.id === selectedId);
    if (!seg || seg.path.length === 0) return;
    const b = new g.maps.LatLngBounds();
    seg.path.forEach((pt) => b.extend(pt));
    map.fitBounds(b, 120);
  }, [ready, selectedId, effectiveSegments]);

  // Park roads coloured by their surface risk: every park's network, each from the run in use for that park.
  const networkKey = networkRuns.map((r) => `${r.place}/${r.date}`).join('|');
  useEffect(() => {
    const map = mapRef.current;
    if (!ready || !map) return;
    map.data.forEach((f) => map.data.remove(f));
    if (!layers.network || !networkKey) return;
    let cancelled = false;
    map.data.setStyle((f) => ({
      strokeColor: riskHex(Number(f.getProperty('surfaceRisk'))),
      strokeOpacity: 0.75,
      strokeWeight: 2,
      clickable: false,
      zIndex: 1,
    }));
    for (const key of networkKey.split('|')) {
      const [place, date] = key.split('/');
      if (!networkCache.has(key)) networkCache.set(key, getRoadNetwork(date, place));
      networkCache
        .get(key)!
        .then((network) => {
          // A border road in two parks has the same id in both; the Data layer keeps one feature per id.
          if (!cancelled) map.data.addGeoJson({ type: 'FeatureCollection', features: network.features });
        })
        .catch(() => networkCache.delete(key));
    }
    return () => {
      cancelled = true;
    };
  }, [ready, layers.network, networkKey]);

  const selected = segments.find((s) => s.id === selectedId);
  const selectedPrediction = selected ? predictions[selected.id] : undefined;

  return (
    <div className="relative h-full w-full overflow-hidden bg-map">
      <div ref={containerRef} className="absolute inset-0 h-full w-full" />

      {!ready && !error && (
        <div className="absolute inset-0 flex items-center justify-center text-sm text-muted-foreground">
          Loading map…
        </div>
      )}
      {error && (
        <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-sm text-destructive">
          {error}
        </div>
      )}

      <LayerToggles layers={layers} onToggle={toggle} />

      {layers.sightings && (
        <SightingsPanel
          run={sightings.run}
          error={sightings.error}
          species={selectedSpecies}
          onSpecies={setSpecies}
          layer={sightingsLayer}
          loadingTiles={loadingTiles}
        />
      )}

      {selected && detailOpen && (
        <SegmentDetailCard
          segment={selected}
          prediction={selectedPrediction}
          onClose={() => setDetailOpen(false)}
        />
      )}

      <div className="pointer-events-none absolute bottom-[4.5rem] left-3 z-10 rounded-lg border border-border bg-card/90 p-2 text-[10px] shadow-md backdrop-blur sm:bottom-24 sm:left-4 sm:p-3 sm:text-xs">
        <div className="mb-1 font-semibold text-foreground sm:mb-1.5">Surface risk</div>
        <div className="mb-1 hidden text-[10px] text-muted-foreground sm:block">Rule-based from recent rain, unvalidated</div>
        <div className="flex items-center gap-1.5">
          <span className="h-2 w-6 rounded-full bg-condition-good" /> Low (up to 30%)
        </div>
        <div className="flex items-center gap-1.5">
          <span className="h-2 w-6 rounded-full bg-condition-warn" /> Medium (30–60%)
        </div>
        <div className="flex items-center gap-1.5">
          <span className="h-2 w-6 rounded-full bg-condition-bad" /> High (over 60%)
        </div>
        <div className="flex items-center gap-1.5">
          <span className="h-2 w-6 rounded-full" style={{ background: NO_DATA_HEX }} /> No data (off mapped park roads)
        </div>
      </div>
    </div>
  );
}
