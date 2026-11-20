import { useState } from 'react';
import { RoadTripView } from '@/components/RoadTrip/RoadTripView';
import { useTripData } from '@/hooks/useTripData';

const todayUtc = () => new Date().toISOString().slice(0, 10);

/** One map: the trip's routes with their road risk, and the sightings heat map as a layer on top. */
export default function App() {
  const tripData = useTripData();
  const [date, setDate] = useState(todayUtc);

  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden bg-background text-foreground">
      <nav className="flex h-10 shrink-0 items-center gap-6 border-b border-border bg-card px-4 md:h-12 md:px-5">
        <span className="text-[11px] font-bold uppercase tracking-[0.18em] text-primary">Safari Explorer</span>
      </nav>

      <RoadTripView {...tripData} date={date} onDate={setDate} />
    </div>
  );
}
