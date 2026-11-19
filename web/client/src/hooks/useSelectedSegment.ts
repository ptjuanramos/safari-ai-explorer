import { useState, useCallback } from 'react';

export function useSelectedSegment() {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const select = useCallback((id: string | null) => setSelectedId(id), []);
  const clear = useCallback(() => setSelectedId(null), []);
  return { selectedId, select, clear };
}
