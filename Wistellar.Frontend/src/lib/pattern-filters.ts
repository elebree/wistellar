// The filter's text patterns, `ssid` and `bssid`: SQL LIKE, `%` for any text and `_` for one
// character, with `|` between alternatives. The server matches them case-insensitively. Both
// fields hold something different for each kind of network, so the titles don't use WiGLE's
// WiFi terms.

import { getFilterParam, setFilterParam } from "./filter-query.ts";
import tagIcon from "./icons/tag.svg?raw";
import hashtagIcon from "./icons/hashtag.svg?raw";

export interface PatternFilter {
  /** The query parameter. */
  name: string;
  /** Title, for every kind of network. */
  label: string;
  /** One line under the title on what the field holds for each kind of network. */
  hint: string;
  /** SVG markup for `Icon`. */
  icon: string;
  /** The page's default pattern, which the short form leaves out and Reset brings back. */
  initial?: string;
}

export const patternFilters: PatternFilter[] = [
  {
    name: "ssid",
    label: "Name",
    hint: "SSID, Bluetooth device name or cell operator",
    icon: tagIcon,
    // Every network with a name.
    initial: "_%",
  },
  {
    name: "bssid",
    label: "MAC or cell ID",
    // The importers make the cell ID from the PLMN, the area code and the cell: 26201_12345_678.
    hint: "MAC address, or MCCMNC_LAC_CID for a cell",
    icon: hashtagIcon,
  },
];

export const patternSyntax = "% for any text, _ for one character, | between alternatives";

/** The pattern as it stands in the filter; empty without one. */
export function patternOf(filter: string, pattern: PatternFilter): string {
  return getFilterParam(filter, pattern.name) ?? "";
}

/** Sets the pattern. An empty one removes the parameter, which matches every network. */
export function filterWithPattern(filter: string, pattern: PatternFilter, value: string): string {
  const trimmed = value.trim();
  return setFilterParam(filter, pattern.name, trimmed === "" ? undefined : trimmed);
}

/** Whether the pattern narrows the map more than the page's default does. */
export function patternLimited(filter: string, pattern: PatternFilter): boolean {
  return !["", "%", pattern.initial].includes(patternOf(filter, pattern));
}

/** The patterns that narrow the map, with their values, for the short form. */
export function limitedPatterns(filter: string): { pattern: PatternFilter; value: string }[] {
  return patternFilters
    .filter((pattern) => patternLimited(filter, pattern))
    .map((pattern) => ({ pattern, value: patternOf(filter, pattern) }));
}

/** Takes back every pattern that narrows the map: to the default, or out of the filter. */
export function filterWithoutPatterns(filter: string): string {
  return patternFilters
    .filter((pattern) => patternLimited(filter, pattern))
    .reduce((result, pattern) => filterWithPattern(result, pattern, pattern.initial ?? ""), filter);
}
