<script lang="ts">
  import "maplibre-gl/dist/maplibre-gl.css";
  import { onMount, onDestroy, untrack, mount, unmount } from "svelte";
  import { page } from "$app/state";
  import { api, handleMapError, transformRequest } from "#lib/api.ts";
  import NetworkPopup from "./NetworkPopup.svelte";

  import mapLibre, {
    type GeoJSONSource,
    type MapLayerMouseEvent,
    type Subscription,
    type VectorTileSource,
  } from "maplibre-gl";
  import type { Feature, FeatureCollection } from "geojson";

  const { Map, NavigationControl, GeolocateControl, Popup } = mapLibre;

  interface Props {
    filter: string;
    zoom: number;
    lng: number;
    lat: number;
    /** Network tiles need a signed-in user, so their source and layers exist only while this is true. */
    signedIn: boolean;
    onChangeView?: (zoom: number, lat: number, lng: number) => void;
  }

  let { filter, zoom, lng, lat, signedIn, onChangeView }: Props = $props();

  let map: mapLibre.Map;
  let mapContainer: HTMLElement;
  let styleLoaded = $state(false);
  let selection = $state<string>();
  let popup: mapLibre.Popup | undefined;
  let networkSubscriptions: Subscription[] = [];

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
  const networkLayerIds = layers.map(([layer]) => `networks-${layer}`);

  $effect(() => changeScale(zoom, lat, lng));
  function changeScale(zoom: number, lat: number, lng: number) {
    if (map) {
      map.setCenter({ lat, lng });
      map.setZoom(zoom);
    }
  }

  // Reactive updates
  $effect(() => updateFilter(filter));
  $effect(() => {
    updateSelection(selection);
  });
  $effect(() => {
    if (!styleLoaded) return;
    if (signedIn) addNetworkLayers();
    else removeNetworkLayers();
  });

  function updateFilter(filter: string) {
    selection = undefined;
    const networkSource = map?.getSource(networksSource) as
      | VectorTileSource
      | undefined;
    networkSource?.setTiles([`${networkTilesUrl}?${filter}`]);
  }

  async function updateSelection(bssid: string | undefined) {
    const source = map?.getSource(geojsonSource) as GeoJSONSource | undefined;

    if (!bssid) {
      source?.setData({ type: "FeatureCollection", features: [] });
      return;
    }

    let data: FeatureCollection;
    try {
      data = await getLocationInfo(bssid);
    } catch (error) {
      console.error(error);
      return;
    }
    // The selection may have changed, or been cleared by a sign-out, while this was loading.
    if (selection !== bssid) return;

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

  async function getNetworkInfo(id: string): Promise<Feature | undefined> {
    const res = await api.get<FeatureCollection>(
      networkInfoUrl.replaceAll("{bssid}", id),
    );
    return res.data.features[0];
  }

  async function getLocationInfo(id: string): Promise<FeatureCollection> {
    const res = await api.get<FeatureCollection>(
      locationsUrl.replaceAll("{bssid}", id),
    );
    return res.data;
  }

  function addNetworkLayers() {
    if (map.getSource(networksSource)) return;

    map.addSource(networksSource, {
      type: "vector",
      maxzoom: 14,
      tiles: [`${networkTilesUrl}?${untrack(() => filter)}`],
    });

    for (const [layer, color] of layers) {
      // Below the selection layer, so a selected network's observations stay on top.
      map.addLayer(
        {
          id: `networks-${layer}`,
          type: "circle",
          source: networksSource,
          "source-layer": layer,
          paint: {
            "circle-radius": 4,
            "circle-color": color,
          },
        },
        geojsonSource,
      );
    }

    // One listener for all three layers, so a click on overlapping points opens one popup.
    networkSubscriptions = [
      map.on("click", networkLayerIds, onNetworkClick),
      map.on("mouseenter", networkLayerIds, () => {
        map.getCanvas().style.cursor = "pointer";
      }),
      map.on("mouseleave", networkLayerIds, () => {
        map.getCanvas().style.cursor = "";
      }),
    ];
  }

  function removeNetworkLayers() {
    for (const subscription of networkSubscriptions) subscription.unsubscribe();
    networkSubscriptions = [];

    for (const id of networkLayerIds) {
      if (map.getLayer(id)) map.removeLayer(id);
    }
    if (map.getSource(networksSource)) map.removeSource(networksSource);

    map.getCanvas().style.cursor = "";
    popup?.remove();
    popup = undefined;
    selection = undefined;
  }

  async function onNetworkClick(e: MapLayerMouseEvent) {
    const feature = e.features?.[0];
    if (!feature) return;

    const id = feature.id ?? feature.properties?._NetTopologySuite_id;
    selection = id;

    let info: Feature | undefined;
    try {
      info = await getNetworkInfo(id);
    } catch (error) {
      console.error(error);
      return;
    }
    // Another click, a filter change or a sign-out happened while this was loading.
    if (selection !== id) return;

    const properties = info?.properties ?? { bssid: id };

    popup?.remove();
    const current = new Popup({
      className: "network-popup",
      closeButton: false,
      maxWidth: "none",
      offset: 10,
    });
    const content = document.createElement("div");
    const component = mount(NetworkPopup, {
      target: content,
      props: { properties, onclose: () => current.remove() },
    });
    current.on("close", () => unmount(component));
    current.setLngLat(e.lngLat).setDOMContent(content).addTo(map);
    popup = current;
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
    map.on("error", (ev) => {
      if (!handleMapError(ev)) console.error(ev);
    });
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

      // The network layers are added by an effect once the style is ready, if signed in.
      styleLoaded = true;
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

  /* NetworkPopup brings its own panel, so strip MapLibre's box and tip around it. */
  :global(.network-popup .maplibregl-popup-content) {
    padding: 0;
    background: none;
    border-radius: 0;
    box-shadow: none;
  }

  :global(.network-popup .maplibregl-popup-tip) {
    display: none;
  }
</style>
