// Network types, the map layers they are drawn in, and the filter's `type` parameter, which lists
// the types to show separated by `|` (`type=W|E|B`). Without it, or with it empty, the server shows
// every type.

import { decode, getFilterParam, setFilterParam } from "./filter-query.ts";
import wifiIcon from "./icons/network-types/wifi.svg?raw";
import btIcon from "./icons/network-types/bt.svg?raw";
import bleIcon from "./icons/network-types/ble.svg?raw";
import gsmIcon from "./icons/network-types/gsm.svg?raw";
import cdmaIcon from "./icons/network-types/cdma.svg?raw";
import icon3g from "./icons/network-types/3g.svg?raw";
import icon4g from "./icons/network-types/4g.svg?raw";
import icon5g from "./icons/network-types/5g.svg?raw";
import nfcIcon from "./icons/network-types/nfc.svg?raw";

/** The layers /geo/tiles splits networks into, with the color each is drawn in. */
export const networkLayers = {
  wifi: "tomato",
  bluetooth: "dodgerblue",
  cell: "darkolivegreen",
} as const;

export type NetworkLayer = keyof typeof networkLayers;

export interface NetworkType {
  /** The letter in the `network.type` column and the filter. */
  code: string;
  /** The text the icon shows, which also names its toggle. */
  badge: string;
  /** Full name, for the tooltip. */
  label: string;
  layer: NetworkLayer;
  /** SVG markup for `Icon`: the badge text, set in Carlito Bold and converted to paths, filling the badge. */
  icon: string;
}

/** The types that have a toggle, in toggle order. */
export const networkTypes: readonly NetworkType[] = [
  { code: "W", badge: "WiFi", label: "WiFi", layer: "wifi", icon: wifiIcon },
  { code: "B", badge: "BT", label: "Bluetooth", layer: "bluetooth", icon: btIcon },
  { code: "E", badge: "BLE", label: "Bluetooth LE", layer: "bluetooth", icon: bleIcon },
  { code: "G", badge: "GSM", label: "GSM", layer: "cell", icon: gsmIcon },
  { code: "C", badge: "CDMA", label: "CDMA", layer: "cell", icon: cdmaIcon },
  { code: "D", badge: "3G", label: "UMTS (3G)", layer: "cell", icon: icon3g },
  { code: "L", badge: "4G", label: "LTE (4G)", layer: "cell", icon: icon4g },
  { code: "N", badge: "5G", label: "5G NR", layer: "cell", icon: icon5g },
];

// NFC has no toggle, and isn't in the default filter; a filter typed with it keeps it.
const nfc: NetworkType = { code: "F", badge: "NFC", label: "NFC", layer: "bluetooth", icon: nfcIcon };

/** The type with this code, or `undefined` for a code the app doesn't know. */
export function networkTypeOf(code: unknown): NetworkType | undefined {
  return [...networkTypes, nfc].find((type) => type.code === code);
}

/** The color of the type's map layer, for a badge's `--type-color`. */
export const typeColor = (type: NetworkType | undefined) => (type ? networkLayers[type.layer] : undefined);

const TYPE = "type";

/** The types the filter shows: the listed ones, or every type with a toggle when none are listed. */
export function shownTypes(filter: string): string[] {
  const listed = decode(getFilterParam(filter, TYPE) ?? "").split("|").filter((code) => code !== "");
  return listed.length > 0 ? listed : networkTypes.map((type) => type.code);
}

/**
 * The filter with one type switched on or off. The list is written in toggle order, followed by
 * any other codes as typed. The last type can't be switched off: an empty list shows everything.
 */
export function filterWithTypeToggled(filter: string, code: string): string {
  const shown = shownTypes(filter);
  const next = shown.includes(code) ? shown.filter((c) => c !== code) : [...shown, code];
  if (next.length === 0) return filter;

  const order = networkTypes.map((type) => type.code);
  const known = order.filter((c) => next.includes(c));
  const others = next.filter((c) => !order.includes(c));
  return setFilterParam(filter, TYPE, [...known, ...others].join("|"));
}

/** Whether the filter shows fewer types than all that have a toggle (or others than those). */
export function typesLimited(filter: string): boolean {
  const shown = shownTypes(filter);
  return shown.length !== networkTypes.length || networkTypes.some((type) => !shown.includes(type.code));
}

/** The filter showing every type that has a toggle, as the default filter does. */
export function filterWithAllTypes(filter: string): string {
  return setFilterParam(filter, TYPE, networkTypes.map((type) => type.code).join("|"));
}
