<script lang="ts">
  import "maplibre-gl/dist/maplibre-gl.css";
  import { onMount, onDestroy, untrack, type Snippet } from "svelte";
  import { page } from "$app/state";
  import { api, handleMapError, transformRequest } from "#lib/api.ts";
  import { hideSplash } from "#lib/splash.ts";
  import { networkLayers } from "#lib/network-types.ts";
  import { toQueryString } from "#lib/filter-query.ts";
  import { attributionCookie, readCookie, writeCookie } from "#lib/ui-cookies.ts";

  import {
    Map,
    NavigationControl,
    GeolocateControl,
    setWorkerUrl,
    type GeoJSONSource,
    type MapMouseEvent,
    type MapGeoJSONFeature,
    type IControl,
    type Subscription,
  } from "maplibre-gl";
  import workerUrl from "maplibre-gl/dist/maplibre-gl-worker.mjs?url";
  import type { Feature, FeatureCollection } from "geojson";

  setWorkerUrl(workerUrl);

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
    /**
     * The page's own buttons for the column down the right edge, on top of the zoom buttons. The
     * page gives them and the map's buttons one look.
     */
    controls?: Snippet;
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
    controls,
  }: Props = $props();

  let map: Map;
  let mapContainer: HTMLElement;
  let controlsElement = $state<HTMLElement>();
  let styleLoaded = $state(false);
  /** Facing north, flat and level: the compass has nothing to reset and hides. */
  let northUp = $state(true);
  let selection = $state<string>();
  let networkSubscriptions: Subscription[] = [];

  /*
   * Every filter, and every reload after an import, gets a vector source of its own: `networks-1`,
   * `networks-2`, … MapLibre's `setTiles` on one source would refetch each tile without cancelling
   * the request still running for the old URL, and draw that one's response too when it arrives,
   * so quick filter changes piled up requests. Removing a source does cancel the requests of its
   * tiles that are still loading.
   *
   * The newest source that has finished loading stays on screen until the next one has, so the
   * networks don't blink out on every change. Any other older source goes at once.
   */
  let networkGeneration = 0;
  /** Oldest first; the last one has the current filter. At most the shown one and the current one. */
  let networkSources: string[] = [];
  let shownNetworkSource: string | undefined;
  let pointerOverNetwork = false;

  const geojsonSource = "geojson";
  // The API is served from the same origin as the SPA.
  const apiHost = page.url.protocol + "//" + page.url.host;
  const networkTilesUrl = `${apiHost}/geo/tiles/{z}/{x}/{y}.pbf`;
  const networkInfoUrl = `${apiHost}/geo/network?bssid={bssid}`;
  const locationsUrl = `${apiHost}/geo/location?bssid={bssid}`;
  const mapStyle = "liberty.json";

  let locationsMinLevel = -100;

  // Drawn bottom to top: bluetooth, wifi, cell.
  const layerNames = ["bluetooth", "wifi", "cell"] as const;
  const layerIdsOf = (source: string) => layerNames.map((layer) => `${source}-${layer}`);
  const networkLayerIds = () => networkSources.flatMap(layerIdsOf);

  $effect(() => changeScale(zoom, lat, lng));
  function changeScale(zoom: number, lat: number, lng: number) {
    if (map) {
      map.setCenter({ lat, lng });
      map.setZoom(zoom);
    }
  }

  // Reactive updates
  $effect(() => {
    updateSelection(selection);
  });
  $effect(() => {
    if (!styleLoaded) return;
    if (signedIn) untrack(startNetworks);
    else untrack(stopNetworks);
  });
  $effect(() => {
    const current = filter;
    untrack(() => {
      selection = undefined;
      if (networkSources.length > 0) loadNetworks(current);
    });
  });
  $effect(() => {
    if (dataVersion === 0) return;
    untrack(() => {
      network = undefined;
      selection = undefined;
      if (networkSources.length > 0) loadNetworks(filter);
    });
  });

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

  function startNetworks() {
    if (networkSources.length > 0) return;

    networkSubscriptions = [
      map.on("click", onMapClick),
      map.on("mousemove", onMapMouseMove),
      map.on("sourcedata", (e) => {
        if (e.sourceId === networkSources.at(-1)) settleNetworks();
      }),
      // A failed tile fires no `sourcedata`; the source still counts as loaded then.
      map.on("idle", settleNetworks),
    ];
    loadNetworks(filter);
  }

  function stopNetworks() {
    for (const subscription of networkSubscriptions) subscription.unsubscribe();
    networkSubscriptions = [];

    for (const source of networkSources) removeNetworkSource(source);

    pointerOverNetwork = false;
    map.getCanvas().style.cursor = "";
    network = undefined;
    selection = undefined;
  }

  /** Loads the networks for `filter` into a new source, and drops older ones that are still loading. */
  function loadNetworks(filter: string) {
    const source = `networks-${++networkGeneration}`;
    map.addSource(source, {
      type: "vector",
      maxzoom: 14,
      tiles: [`${networkTilesUrl}?${toQueryString(filter)}`],
    });

    for (const layer of layerNames) {
      // Below the selection layer, so a selected network's observations stay on top. That also
      // puts them above the older sources' layers.
      map.addLayer(
        {
          id: `${source}-${layer}`,
          type: "circle",
          source,
          "source-layer": layer,
          paint: {
            "circle-radius": 4,
            "circle-color": networkLayers[layer],
          },
        },
        geojsonSource,
      );
    }

    for (const old of networkSources) {
      if (old !== shownNetworkSource) removeNetworkSource(old);
    }
    networkSources.push(source);
  }

  /** Once the current source has loaded, it is the one shown, and the older one can go. */
  function settleNetworks() {
    const current = networkSources.at(-1);
    if (!current || current === shownNetworkSource || !map.isSourceLoaded(current)) return;
    shownNetworkSource = current;
    for (const old of networkSources.slice(0, -1)) removeNetworkSource(old);
  }

  function removeNetworkSource(source: string) {
    for (const id of layerIdsOf(source)) {
      if (map.getLayer(id)) map.removeLayer(id);
    }
    if (map.getSource(source)) map.removeSource(source);
    networkSources = networkSources.filter((s) => s !== source);
    if (shownNetworkSource === source) shownNetworkSource = undefined;
  }

  /** A pointer cursor over a network. Layers come and go with each filter, so this queries them all. */
  function onMapMouseMove(e: MapMouseEvent) {
    const over = map.queryRenderedFeatures(e.point, { layers: networkLayerIds() }).length > 0;
    if (over === pointerOverNetwork) return;
    pointerOverNetwork = over;
    map.getCanvas().style.cursor = over ? "pointer" : "";
  }

  /**
   * One listener for the whole map: a click on a network selects it, even where points of several
   * layers overlap, and a click anywhere else closes the details.
   */
  function onMapClick(e: MapMouseEvent) {
    const [feature] = map.queryRenderedFeatures(e.point, { layers: networkLayerIds() });
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

  function keepAttributionCollapsed(container: HTMLElement) {
    const attribution = container.querySelector<HTMLElement>(".maplibregl-ctrl-attrib");
    if (!attribution) return;
    const shown = "maplibregl-compact-show";
    let hidden = readCookie(attributionCookie) === "hidden";
    const collapse = () => {
      if (hidden && attribution.classList.contains(shown)) attribution.classList.remove(shown);
    };
    // Captured before MapLibre's listener on the button toggles the class: the observer runs in
    // between, so it has to know the user's choice by then.
    attribution.addEventListener(
      "click",
      (event) => {
        const button = (event.target as Element).closest(".maplibregl-ctrl-attrib-button");
        if (!button || !attribution.classList.contains("maplibregl-compact")) return;
        hidden = attribution.classList.contains(shown);
        writeCookie(attributionCookie, hidden ? "hidden" : "shown");
      },
      { capture: true },
    );
    new MutationObserver(collapse).observe(attribution, { attributeFilter: ["class"] });
    collapse();
  }

  /** A MapLibre control made of an element that already exists. */
  function elementControl(element: HTMLElement): IControl {
    return {
      onAdd: () => element,
      onRemove: () => element.remove(),
    };
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
    keepAttributionCollapsed(map.getContainer());
    const onViewChangedEvent = () => {
      const zoom = map.getZoom();
      const center = map.getCenter();
      if (onChangeView) onChangeView(zoom, center.lat, center.lng);
    };

    map.on("zoomend", onViewChangedEvent);
    map.on("dragend", onViewChangedEvent);

    map.on("move", () => {
      northUp = [map.getBearing(), map.getPitch(), map.getRoll()].every((angle) => Math.abs(angle) < 0.01);
    });

    // MapLibre stacks a bottom corner's controls upwards, each added one on top of the last. Down
    // the right edge that makes: the page's controls, zoom, location, compass. Zoom and compass are
    // two navigation controls so that the location button can go between them.
    map.addControl(
      new NavigationControl({
        visualizePitch: true,
        visualizeRoll: true,
        showZoom: false,
        showCompass: true,
      }),
      "bottom-right",
    );

    map.addControl(
      new GeolocateControl({
        positionOptions: { enableHighAccuracy: true },
        trackUserLocation: true,
      }),
      "bottom-right",
    );

    map.addControl(new NavigationControl({ showZoom: true, showCompass: false }), "bottom-right");

    // The element stays Svelte's: MapLibre only moves it into the corner.
    if (controlsElement) map.addControl(elementControl(controlsElement), "bottom-right");

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

<div class="map-wrap" class:north-up={northUp}>
  <div class="map" bind:this={mapContainer}></div>
  {#if controls}
    <!-- Moved into the map's bottom right corner once the map is set up. -->
    <div class="maplibregl-ctrl page-controls" bind:this={controlsElement}>
      {@render controls()}
    </div>
  {/if}
</div>

<style>
  .map-wrap {
    position: relative;
    width: 100%;
    height: 100%;
  }

  .map {
    position: absolute;
    width: 100%;
    height: 100%;
  }

  /*
   * The column down the right edge: the page's buttons, then MapLibre's zoom, location and compass
   * buttons, 10px apart. The page gives all of them the look of its menu button (`map-button` in
   * +page.svelte); what is MapLibre's own is fitted here: zoom in and zoom out make one capsule of
   * two halves, and the icons are 20px masks made from Lucide icons.
   *
   * Each selector starts with `.map-wrap` and the corner so that it beats MapLibre's own rules,
   * which go up to five classes for the location button's states.
   */

  /* MapLibre keeps translate(0) on every control. It would make the page's controls the box that a
     fixed pane in them is placed in, instead of the window. */
  .map-wrap :global(.maplibregl-ctrl-bottom-right .page-controls) {
    transform: none;
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-group) {
    display: flex;
    flex-direction: column;
    gap: 10px;
    border-radius: 0;
    background: none;
    box-shadow: none;
  }

  /* One capsule: zoom out pulls up under zoom in, so that their edges make a single 1px line. */
  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-group button.maplibregl-ctrl-zoom-in) {
    border-radius: 18px 18px 0 0;
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-group button.maplibregl-ctrl-zoom-out) {
    margin-top: -11px;
    border-radius: 0 0 18px 18px;
  }

  /* MapLibre draws its icons as background images in fixed colors; these are masks over the
     button's text color instead. MapLibre picks the location button's image per state with up to
     five classes, hence the !important. Forced colors keep the mask: the color is the system's. */
  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-group .maplibregl-ctrl-icon) {
    background-color: currentColor;
    background-image: none !important;
    mask: url("lucide-static/icons/locate-fixed.svg") center / 20px no-repeat;
    forced-color-adjust: none;
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-zoom-in .maplibregl-ctrl-icon) {
    mask-image: url("lucide-static/icons/plus.svg");
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-zoom-out .maplibregl-ctrl-icon) {
    mask-image: url("lucide-static/icons/minus.svg");
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-compass .maplibregl-ctrl-icon) {
    background-color: transparent;
    background-image: linear-gradient(
      var(--color-red-600, oklch(57.7% 0.245 27.325)) 50%,
      var(--color-gray-300) 50%
    ) !important;
    mask-image: url("../lib/icons/compass-needle.svg");
  }

  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-compass) {
    transition:
      opacity 0.2s,
      visibility 0.2s;
  }

  .map-wrap.north-up :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-compass) {
    visibility: hidden;
    opacity: 0;
  }

  .map-wrap :global(.maplibregl-ctrl-attrib-button) {
    background-image: none;
    color: var(--color-gray-600);
  }

  .map-wrap :global(.maplibregl-ctrl-attrib-button::before) {
    content: "";
    position: absolute;
    inset: 0;
    background-color: currentColor;
    mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill-rule='evenodd' viewBox='0 0 20 20'%3E%3Cpath d='M4 10a6 6 0 1 0 12 0 6 6 0 1 0-12 0m5-3a1 1 0 1 0 2 0 1 1 0 1 0-2 0m0 3a1 1 0 1 1 2 0v3a1 1 0 1 1-2 0'/%3E%3C/svg%3E")
      center / 24px no-repeat;
  }

  /* The map no longer follows the location: no dot. */
  .map-wrap :global(.maplibregl-ctrl-bottom-right .maplibregl-ctrl-group .maplibregl-ctrl-geolocate-background) {
    color: var(--color-indigo-600);
  }

  .map-wrap
    :global(
      .maplibregl-ctrl-bottom-right
        :is(.maplibregl-ctrl-geolocate-background, .maplibregl-ctrl-geolocate-background-error)
        .maplibregl-ctrl-icon
    ) {
    mask-image: url("lucide-static/icons/locate.svg");
  }

  .map-wrap
    :global(
      .maplibregl-ctrl-bottom-right
        .maplibregl-ctrl-group
        :is(.maplibregl-ctrl-geolocate-active-error, .maplibregl-ctrl-geolocate-background-error)
    ) {
    color: var(--color-red-600, oklch(57.7% 0.245 27.325));
  }
</style>
