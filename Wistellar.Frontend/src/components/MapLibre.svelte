<script lang="ts">
  import "maplibre-gl/dist/maplibre-gl.css";
  import { onMount, onDestroy, untrack } from "svelte";
  import { page } from "$app/state";
  import { api, handleMapError, transformRequest } from "#lib/api.ts";
  import { hideSplash } from "#lib/splash.ts";

  import mapLibre, {
    type GeoJSONSource,
    type MapMouseEvent,
    type MapGeoJSONFeature,
    type Subscription,
    type VectorTileSource,
  } from "maplibre-gl";
  import type { Feature, FeatureCollection } from "geojson";

  const { Map, NavigationControl, GeolocateControl } = mapLibre;

  interface Props {
    filter: string;
    zoom: number;
    lng: number;
    lat: number;
    /** Network tiles need a signed-in user, so their source and layers exist only while this is true. */
    signedIn: boolean;
    /** Incremented after an import to reload the visible network tiles. */
    dataVersion?: number;
    onChangeView?: (zoom: number, lat: number, lng: number) => void;
    /**
     * Details of the network last clicked, for the page to show. Cleared by a click on an empty spot of
     * the map, an import and a sign-out; the page clears it to close its panel.
     */
    network?: Record<string, unknown>;
  }

  let {
    filter,
    zoom,
    lng,
    lat,
    signedIn,
    dataVersion = 0,
    onChangeView,
    network = $bindable(),
  }: Props = $props();

  let map: mapLibre.Map;
  let mapContainer: HTMLElement;
  let styleLoaded = $state(false);
  let selection = $state<string>();
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
  $effect(() => {
    if (!styleLoaded || !signedIn || dataVersion === 0) return;
    untrack(() => {
      network = undefined;
      selection = undefined;
      map.refreshTiles(networksSource);
    });
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

    networkSubscriptions = [
      map.on("click", onMapClick),
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
    network = undefined;
    selection = undefined;
  }

  /**
   * One listener for the whole map: a click on a network selects it, even where points of several
   * layers overlap, and a click anywhere else closes the details.
   */
  function onMapClick(e: MapMouseEvent) {
    const [feature] = map.queryRenderedFeatures(e.point, { layers: networkLayerIds });
    if (feature) selectNetwork(feature);
    else network = undefined;
  }

  async function selectNetwork(feature: MapGeoJSONFeature) {
    const id = feature.id ?? feature.properties?._NetTopologySuite_id;
    selection = id;

    let info: Feature | undefined;
    try {
      info = await getNetworkInfo(id);
    } catch (error) {
      console.error(error);
      // Don't leave the previous network's details up next to this network's observations.
      if (selection === id) network = undefined;
      return;
    }
    // Another click, a filter change or a sign-out happened while this was loading.
    if (selection !== id) return;

    network = info?.properties ?? { bssid: id };
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
      // The base map has drawn for the first time, so the startup screen can go.
      hideSplash();
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
