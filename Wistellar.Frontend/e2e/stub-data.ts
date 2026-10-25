import type { FeatureCollection, Point } from "geojson";

// Stub networks, shaped like the /geo/network response. 02 and 03 share a position on purpose: a
// wifi and a bluetooth point on top of each other.
export const networks: FeatureCollection<Point> = {
  type: "FeatureCollection",
  features: [
    {
      type: "Feature",
      id: "00:00:00:00:00:01",
      geometry: { type: "Point", coordinates: [13.4, 52.52] },
      properties: {
        type: "W",
        bssid: "00:00:00:00:00:01",
        ssid: "Stub WiFi One",
        cap: "[WPA2-PSK-CCMP][ESS]",
        lasttime: 1792885194000,
        lastlat: 52.52,
        lastlon: 13.4,
        level: -50,
        loc: 3,
        dist: 0,
        dur: 3725,
        vendor: "Stub Vendor",
      },
    },
    {
      type: "Feature",
      id: "00:00:00:00:00:02",
      geometry: { type: "Point", coordinates: [13.38, 52.51] },
      properties: {
        type: "W",
        bssid: "00:00:00:00:00:02",
        ssid: "Stub Overlap WiFi",
        cap: "[ESS]",
        lasttime: 1792885194000,
        lastlat: 52.51,
        lastlon: 13.38,
        level: -70,
        loc: 1,
        dist: 0,
        dur: 0,
        vendor: null,
      },
    },
    {
      type: "Feature",
      id: "00:00:00:00:00:03",
      geometry: { type: "Point", coordinates: [13.38, 52.51] },
      properties: {
        type: "E",
        bssid: "00:00:00:00:00:03",
        ssid: "Stub Beacon",
        cap: "Misc [LE]",
        lasttime: 1792885194000,
        lastlat: 52.51,
        lastlon: 13.38,
        level: -65,
        loc: 1,
        dist: 0,
        dur: 0,
        vendor: null,
      },
    },
    {
      type: "Feature",
      id: "00:00:00:00:00:04",
      geometry: { type: "Point", coordinates: [13.42, 52.53] },
      properties: {
        type: "W",
        bssid: "00:00:00:00:00:04",
        ssid: '<b>Stub & "Markup"</b><img src=x onerror=window.__xss=1>',
        cap: "[WPA2-PSK-CCMP][WPS][ESS]",
        lasttime: 1792885194000,
        lastlat: 52.53,
        lastlon: 13.42,
        level: -45,
        loc: 2,
        dist: 12,
        dur: 328,
        vendor: null,
      },
    },
  ],
};

/** Served instead of liberty.json, so the tests don't depend on the basemap service. */
export const style = {
  version: 8,
  name: "Plain background for tests",
  sources: {},
  layers: [{ id: "background", type: "background", paint: { "background-color": "#e5e3df" } }],
};
