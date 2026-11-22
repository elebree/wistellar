// Numbers as the UI shows them, the same in the network details and the filters.

const durationUnits = [
  ["y", 365 * 86400],
  ["d", 86400],
  ["h", 3600],
  ["min", 60],
  ["s", 1],
] as const;

/** Seconds in the two largest units that apply: "1 h 2 min", "7 d", "0 s". */
export function formatDuration(seconds: number): string {
  let rest = Math.round(seconds);
  const amounts = durationUnits.map(([unit, size]) => {
    const amount = Math.floor(rest / size);
    rest -= amount * size;
    return { amount, unit };
  });
  const first = amounts.findIndex((a) => a.amount > 0);
  if (first < 0) return "0 s";
  return amounts
    .slice(first, first + 2)
    .filter((a) => a.amount > 0)
    .map((a) => `${a.amount} ${a.unit}`)
    .join(" ");
}

/** Metres, in kilometres from 1000 on: "250 m", "2.5 km". */
export function formatDistance(metres: number): string {
  return metres < 1000 ? `${Math.round(metres)} m` : `${Math.round(metres / 100) / 10} km`;
}
