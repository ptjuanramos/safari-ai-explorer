export interface LayerState {
  network: boolean;
  sightings: boolean;
}

interface Props {
  layers: LayerState;
  onToggle: (key: keyof LayerState) => void;
}

const ITEMS: { key: keyof LayerState; icon: string; label: string }[] = [
  { key: 'network', icon: '🗺', label: 'Park roads by surface risk' },
  { key: 'sightings', icon: '🦁', label: 'Where visitors see wildlife (heat map)' },
];

export function LayerToggles({ layers, onToggle }: Props) {
  return (
    <div className="pointer-events-auto absolute right-4 top-4 z-10 flex flex-col gap-1.5 rounded-xl border border-border bg-card/90 p-1.5 shadow-lg backdrop-blur">
      {ITEMS.map((item) => {
        const active = layers[item.key];
        return (
          <button
            key={item.key}
            onClick={() => onToggle(item.key)}
            title={item.label}
            aria-pressed={active}
            className={`flex h-9 w-9 items-center justify-center rounded-lg text-base transition-colors ${
              active
                ? 'bg-primary text-primary-foreground'
                : 'bg-transparent text-muted-foreground hover:bg-muted'
            }`}
          >
            <span aria-hidden>{item.icon}</span>
          </button>
        );
      })}
    </div>
  );
}
