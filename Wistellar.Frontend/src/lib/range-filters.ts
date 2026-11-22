// The filter's numeric ranges, each with a `[gt]` and a `[lt]` bound that the server compares
// strictly (`range[gt]=10` is more than 10 m): `range` (metres between the farthest observations),
// `dwell` (seconds between the first and the last one) and `locations` (the number of observations).
// The server recalculates all three after an upload.

import { decode, getFilterParam, setFilterParam } from "./filter-query.ts";
import { parseFilterTime } from "./time-filter.ts";
import { formatDistance, formatDuration } from "./format.ts";
import distanceIcon from "./icons/distance.svg?raw";
import clockIcon from "./icons/clock.svg?raw";
import eyeIcon from "./icons/eye.svg?raw";

export interface Stop {
  value: number;
  /** The value as the filter writes it, for a range that isn't `counted`. */
  param: string;
}

export interface RangeFilter {
  /** The parameter's name before `[gt]` and `[lt]`, as WiGLE calls it. */
  name: string;
  /** Title, saying what is measured rather than WiGLE's term for it. */
  label: string;
  /** One line under the title on how the server gets the value. */
  hint: string;
  /** What a handle sets, for its accessible name: "Minimum distance". */
  noun: string;
  /** SVG markup for `Icon`. */
  icon: string;
  /**
   * Where a slider handle can stop, ascending. At the first stop the lower handle sets no bound,
   * and at the last one, `Infinity`, the upper handle sets none. Values span orders of magnitude,
   * so the steps grow with them.
   */
  stops: Stop[];
  /**
   * Whole numbers, with bounds that include their value: "at least 2" is `[gt]=1`, "at most 1" is
   * `[lt]=2`. With the server's strict bounds as they are, "under 1" would match nothing, and
   * "over 1, under 2" too. The handles may then meet: "exactly 5".
   */
  counted?: boolean;
  /** The value a parameter in the filter holds, or `undefined` where the server ignores it. */
  read(text: string, now: Date): number | undefined;
  format(value: number): string;
}

const NO_LIMIT: Stop = { value: Infinity, param: "" };

/** Whole numbers, which is all the server takes for `range` and `locations`. */
function readCount(text: string): number | undefined {
  return /^\d+$/.test(text) ? Number(text) : undefined;
}

/** Seconds, or a duration with units (`5m`, `1h`, `7d`; y M d h m) as the server reads it. */
function readDuration(text: string, now: Date): number | undefined {
  if (/^\d+$/.test(text)) return Number(text);
  if (!/\d+[yMdhm]/.test(text)) return undefined;
  const from = parseFilterTime(text, now);
  return from ? (now.getTime() - from.getTime()) / 1000 : undefined;
}

const numbers = (values: number[]): Stop[] => [
  ...values.map((value) => ({ value, param: String(value) })),
  NO_LIMIT,
];

export const rangeFilters: readonly RangeFilter[] = [
  {
    name: "range",
    label: "Seen across",
    hint: "Distance between the farthest places it was seen",
    noun: "distance",
    icon: distanceIcon,
    stops: numbers([0, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]),
    read: readCount,
    format: formatDistance,
  },
  {
    name: "dwell",
    label: "Seen over",
    hint: "Time from its first sighting to its last",
    noun: "time span",
    icon: clockIcon,
    // Days rather than months or years, which the server counts in calendar time.
    stops: [
      { value: 0, param: "0" },
      { value: 60, param: "1m" },
      { value: 300, param: "5m" },
      { value: 900, param: "15m" },
      { value: 3600, param: "1h" },
      { value: 6 * 3600, param: "6h" },
      { value: 86400, param: "1d" },
      { value: 7 * 86400, param: "7d" },
      { value: 30 * 86400, param: "30d" },
      { value: 365 * 86400, param: "365d" },
      NO_LIMIT,
    ],
    read: readDuration,
    format: formatDuration,
  },
  {
    name: "locations",
    label: "Sightings",
    hint: "How many times it was recorded",
    noun: "sightings",
    icon: eyeIcon,
    // Every network has at least one, so the lower handle at 1 sets nothing.
    stops: numbers([1, 2, 3, 5, 10, 25, 50, 100, 250, 500, 1000]),
    counted: true,
    read: readCount,
    format: String,
  },
];

/** A range's limits as the UI shows them: inclusive for `counted` ranges, else exclusive. */
export interface Bounds {
  min?: number;
  max?: number;
}

export type Handle = "min" | "max";

/** The filter's bounds for this range; a bound the server would ignore is left out. */
export function boundsOf(filter: string, range: RangeFilter, now: Date): Bounds {
  const read = (bound: "gt" | "lt") => {
    const text = getFilterParam(filter, `${range.name}[${bound}]`);
    return text === undefined ? undefined : range.read(decode(text).trim(), now);
  };
  const gt = read("gt");
  const lt = read("lt");
  if (!range.counted) return { min: gt, max: lt };
  return { min: gt === undefined ? undefined : gt + 1, max: lt === undefined ? undefined : lt - 1 };
}

/** How many stops the handles keep between them: none where they may meet. */
const handleGap = (range: RangeFilter) => (range.counted ? 0 : 1);

/** The stop nearest to `value`, measured on the same growing scale as the stops. */
function nearestStop(range: RangeFilter, value: number): number {
  const last = range.stops.length - 1;
  if (value === Infinity) return last;
  const scale = (v: number) => Math.log1p(v);
  let best = 0;
  for (let i = 1; i < last; i++) {
    if (Math.abs(scale(range.stops[i].value) - scale(value)) < Math.abs(scale(range.stops[best].value) - scale(value))) {
      best = i;
    }
  }
  return best;
}

/** Where the handles stand for these bounds: the nearest stops, in order. */
export function handlesFor(range: RangeFilter, bounds: Bounds): { lo: number; hi: number } {
  const last = range.stops.length - 1;
  const gap = handleGap(range);
  const hi = bounds.max === undefined ? last : Math.max(gap, nearestStop(range, bounds.max));
  const lo = bounds.min === undefined ? 0 : Math.min(hi - gap, nearestStop(range, bounds.min));
  return { lo, hi };
}

/** Keeps a handle moved to `index` from passing the other one, at `other`. */
export function clampHandle(range: RangeFilter, handle: Handle, index: number, other: number): number {
  const gap = handleGap(range);
  return handle === "min" ? Math.min(index, other - gap) : Math.max(index, other + gap);
}

/** The bound a handle at this stop sets: `undefined` for the lower one at the first stop, the upper one at the last. */
export function boundAt(range: RangeFilter, handle: Handle, index: number): number | undefined {
  const open = handle === "min" ? 0 : range.stops.length - 1;
  return index === open ? undefined : range.stops[index].value;
}

/** The filter with one handle's bound set to its stop, or removed where the handle sets none. */
export function filterWithHandle(filter: string, range: RangeFilter, handle: Handle, index: number): string {
  const name = `${range.name}[${handle === "min" ? "gt" : "lt"}]`;
  const value = boundAt(range, handle, index);
  if (value === undefined) return setFilterParam(filter, name, undefined);
  if (!range.counted) return setFilterParam(filter, name, range.stops[index].param);
  return setFilterParam(filter, name, String(handle === "min" ? value - 1 : value + 1));
}

/** A handle's value for screen readers: "No minimum", "More than 25 m", "At least 2". */
export function describeHandle(range: RangeFilter, handle: Handle, value: number | undefined): string {
  if (value === undefined) return handle === "min" ? "No minimum" : "No maximum";
  const words = range.counted ? { min: "At least", max: "At most" } : { min: "More than", max: "Less than" };
  return `${words[handle]} ${range.format(value)}`;
}

/** "Any", "over 25 m", "under 1 h", "25 m – 1 km"; for counts "at least 2", "at most 1", "exactly 5". */
export function describeBounds(range: RangeFilter, { min, max }: Bounds): string {
  if (min === undefined && max === undefined) return "Any";
  const words = range.counted ? { min: "at least", max: "at most" } : { min: "over", max: "under" };
  if (max === undefined) return `${words.min} ${range.format(min!)}`;
  if (min === undefined) return `${words.max} ${range.format(max)}`;
  if (range.counted && min === max) return `exactly ${range.format(min)}`;
  return `${range.format(min)} – ${range.format(max)}`;
}

/** The ranges the filter limits, with their bounds, in panel order. */
export function boundedRanges(filter: string, now: Date): { range: RangeFilter; bounds: Bounds }[] {
  return rangeFilters
    .map((range) => ({ range, bounds: boundsOf(filter, range, now) }))
    .filter(({ bounds }) => bounds.min !== undefined || bounds.max !== undefined);
}

/** The filter without any of the ranges' bounds. */
export function filterWithoutRanges(filter: string): string {
  for (const range of rangeFilters) {
    filter = setFilterParam(setFilterParam(filter, `${range.name}[gt]`, undefined), `${range.name}[lt]`, undefined);
  }
  return filter;
}
