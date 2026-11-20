interface Props {
  value: string;
  onChange: (date: string) => void;
  loading?: boolean;
}

export function DatePicker({ value, onChange, loading }: Props) {
  return (
    <label className="flex items-center gap-2 text-sm">
      <span className="hidden font-medium text-muted-foreground sm:inline">Departure</span>
      <input
        type="date"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="rounded-md border border-border bg-background px-3 py-1.5 text-sm font-medium text-foreground shadow-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
      />
      {loading && (
        <span className="hidden text-xs text-muted-foreground sm:inline">Updating predictions…</span>
      )}
    </label>
  );
}
