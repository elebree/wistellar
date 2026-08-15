<script lang="ts">
  export const prerender = false;
  export const ssr = false;

  import "maplibre-gl/dist/maplibre-gl.css";
  import { onMount, onDestroy } from "svelte";
  import { page } from "$app/state";
  import authService from "$lib/authService";

  import axios from "axios";
  import mapLibre, {
    type GeoJSONSource,
    type RequestTransformFunction,
    type VectorTileSource,
  } from "maplibre-gl";
  import type { Feature, FeatureCollection } from "geojson";

  const { Map, NavigationControl, GeolocateControl, Popup } = mapLibre;

  let map: mapLibre.Map;
  let mapContainer: HTMLElement;

  export let onerror: ((ev: ErrorEvent) => void) | undefined = undefined;
  export let filter: string;
  export let selection: string | undefined = undefined;
  export let zoom: number;
  export let lng: number;
  export let lat: number;
  export let onChangeView:
    | undefined
    | ((zoom: number, lat: number, lng: number) => void) = undefined;

  const networksSource = "networks";
  const geojsonSource = "geojson";
  // The API is served from the same origin as the SPA.
  const apiHost = page.url.protocol + "//" + page.url.host;
  const networkTilesUrl = `${apiHost}/geo/tiles/{z}/{x}/{y}.pbf`;
  const networkInfoUrl = `${apiHost}/geo/network?bssid={bssid}`;
  const locationsUrl = `${apiHost}/geo/location?bssid={bssid}`;
  const mapStyle = "liberty.json";

  let locationsMinLevel = -100;

  const layers = [
    ["bluetooth", "dodgerblue"],
    ["wifi", "tomato"],
    ["cell", "darkolivegreen"],
  ] as const;

  function escapeHTML(s: string): string {
    if (!s) return "";
    s = String(s);
    const tagsToReplace: { [key: string]: string } = {
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
    };

    return s.replace(/[&<>]/g, (tag: string): string => {
      return tagsToReplace[tag] || tag;
    });
  }

  $: changeScale(zoom, lat, lng);
  function changeScale(zoom: number, lat: number, lng: number) {
    if (map) {
      map.setCenter({ lat, lng });
      map.setZoom(zoom);
    }
  }

  // Reactive updates
  $: if (filter) updateFilter(filter);
  $: if (selection) updateSelection(selection);

  function updateFilter(filter: string) {
    selection = "UNDEFINED";
    const networkSource = map?.getSource(networksSource) as VectorTileSource;
    networkSource?.setTiles([`${networkTilesUrl}?${filter}`]);
  }

  async function updateSelection(bssid: string) {
    const source = map?.getSource(geojsonSource) as GeoJSONSource;

    if (!bssid || bssid === "UNDEFINED") {
      source?.setData({ type: "FeatureCollection", features: [] });
      return;
    }

    const data = await getLocationInfo(bssid);
    const colorize = false;
    if (colorize) {
      const param = "level"; // "altitude", "time"
      const levels = data.features
        .map((v) => v.properties?.[param] as number)
        .filter((v) => v !== undefined);

      const minLevel = Math.min(...levels);
      const maxLevel = Math.max(...levels);
      map.setPaintProperty(geojsonSource, "circle-color", [
        "interpolate",
        ["linear"],
        ["get", param],
        minLevel,
        "black",
        maxLevel,
        "orange",
      ]);
    }
    source?.setData(data);
  }

  const transformRequest: RequestTransformFunction = (url) => {
    if (url.startsWith(apiHost)) {
      const token = authService.getToken();
      if (!token) return;

      return {
        url,
        headers: { Authorization: `Bearer ${token}` },
        credentials: "include",
      };
    }
  };

  async function getNetworkInfo(id: string): Promise<Feature | undefined> {
    const res = await axios.get<FeatureCollection>(
      networkInfoUrl.replaceAll("{bssid}", id),
      { headers: { Authorization: `Bearer ${authService.getToken()}` } },
    );
    return res.data.features[0];
  }

  async function getLocationInfo(id: string): Promise<FeatureCollection> {
    const res = await axios.get<FeatureCollection>(
      locationsUrl.replaceAll("{bssid}", id),
      { headers: { Authorization: `Bearer ${authService.getToken()}` } },
    );
    return res.data;
  }

  function addVectorLayer(id: string, sourceLayer: string, color: string) {
    map.addLayer({
      id,
      type: "circle",
      source: networksSource,
      "source-layer": sourceLayer,
      paint: {
        "circle-radius": 4,
        "circle-color": color,
      },
    });

    map.on("click", id, async (e) => {
      const feature = e.features?.[0];
      if (!feature) return;

      const id = feature.id ?? feature.properties?._NetTopologySuite_id;
      selection = id;

      const info = await getNetworkInfo(id);
      const props = info?.properties ?? { bssid: id };

      const html = Object.entries(props)
        .filter(([_, v]) => v)
        .map(([k, v]) => {
          const key = escapeHTML(k);
          const value =
            k === "lasttime"
              ? new Date(v).toLocaleString("de-DE")
              : escapeHTML(v);
          if (value == "") return "";
          switch (key) {
            case "type":
              return `<span class="inline-flex items-center rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-blue-700/10 ring-inset">${value}</span>&nbsp;`;
            case "bssid":
              return `<span class="inline-flex items-center rounded-md bg-gray-50 px-2 py-1 text-xs font-medium text-gray-600 ring-1 ring-gray-500/10 ring-inset">${value}</span><br/>`;
            case "cap":
              return (
                value
                  .replace(/^\[|\]$/g, "")
                  .split("][")
                  .join(";")
                  .split(";")
                  .filter((v) => v != "")
                  .map(
                    (v) =>
                      `<span class="inline-flex items-center rounded-md bg-green-50 px-2 py-1 text-xs font-medium text-green-700 ring-1 ring-green-600/20 ring-inset">${v}</span>`,
                  )
                  .join("&nbsp;") + "</br>"
              );
            case "ssid":
              return `<span class="inline-flex items-center rounded-md bg-yellow-50 px-2 py-1 text-xs font-medium text-yellow-800 ring-1 ring-yellow-600/20 ring-inset">${value}</span><br/>`;
            case "lastlat":
            case "lastlon":
              return "";
          }
          return `${key}: ${value}<br/>`;
        })
        .join("");

      new Popup().setLngLat(e.lngLat).setHTML(html).addTo(map);
    });

    map.on("mouseenter", id, () => {
      map.getCanvas().style.cursor = "pointer";
    });
    map.on("mouseleave", id, () => {
      map.getCanvas().style.cursor = "";
    });
  }

  function setupMap() {
    map = new Map({
      style: mapStyle,
      center: [lng, lat],
      minZoom: 3,
      zoom: zoom,
      container: mapContainer,
      transformRequest,
    });
    if (onerror) map.on("error", onerror);
    const onViewChangedEvent = () => {
      const zoom = map.getZoom();
      const center = map.getCenter();
      if (onChangeView) onChangeView(zoom, center.lat, center.lng);
    };

    map.on("zoomend", onViewChangedEvent);
    map.on("dragend", onViewChangedEvent);

    // Add zoom and rotation controls to the map.
    map.addControl(
      new NavigationControl({
        visualizePitch: true,
        visualizeRoll: true,
        showZoom: true,
        showCompass: true,
      }),
      "bottom-right",
    );

    const geolocateControl = new GeolocateControl({
      positionOptions: { enableHighAccuracy: true },
      trackUserLocation: true,
    });

    // Add geolocate control to the map.
    map.addControl(geolocateControl, "bottom-right");

    map.on("load", () => {
      map.addSource(networksSource, {
        type: "vector",
        maxzoom: 14,
        tiles: [`${networkTilesUrl}?${filter}`],
      });

      for (const [layer, color] of layers) {
        addVectorLayer(`networks-${layer}`, layer, color);
      }

      map.addSource(geojsonSource, {
        type: "geojson",
        data: {
          type: "FeatureCollection",
          features: [],
        },
      });

      map.addLayer({
        id: geojsonSource,
        type: "circle",
        source: geojsonSource,
        paint: {
          "circle-radius": 4,
          "circle-color": "#B42222",
        },
        filter: ["==", "$type", "Point"],
      });
    });
  }

  onMount(setupMap);
  onDestroy(() => map?.remove());
</script>

<div class="map-wrap">
  <a href="https://github.com/elebree/wistellar" class="watermark">
    <img src="github-mark.svg" alt="Wistellar logo" />
  </a>
  <div class="map" bind:this={mapContainer}></div>
</div>

<style>
  .map-wrap {
    position: relative;
    width: 100%;
    height: 100vh;
  }

  .map {
    position: absolute;
    width: 100%;
    height: 100%;
  }

  .watermark {
    position: absolute;
    left: 10px;
    bottom: 10px;
    z-index: 999;
  }

  .watermark img {
    width: 16px;
  }
</style>
