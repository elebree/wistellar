<script lang="ts">
  import "../app.css";
  import { onMount } from "svelte";
  import MapLibre from "../components/MapLibre.svelte";
  import Login from "../components/Login.svelte";
  import ImportFiles from "../components/ImportFiles.svelte";
  import AppMenu, { type MenuItem } from "../components/AppMenu.svelte";
  import NetworkPanel from "../components/NetworkPanel.svelte";
  import TimeFilter from "../components/TimeFilter.svelte";
  import Filters from "../components/Filters.svelte";
  import FilterSummary from "../components/FilterSummary.svelte";
  import { auth } from "#lib/auth.svelte.ts";
  import { loadSavedFilter, loadSavedView, saveFilter, saveView } from "#lib/saved-view.ts";
  import { filterParams } from "#lib/filter-query.ts";
  import signInIcon from "#lib/icons/sign-in.svg?raw";
  import signOutIcon from "#lib/icons/sign-out.svg?raw";
  import userIcon from "#lib/icons/user.svg?raw";
  import uploadIcon from "#lib/icons/upload.svg?raw";
  import Icon from "../components/Icon.svelte";
  import { page } from "$app/state";
  import { goto } from "$app/navigation";

  let lat = $state<number>(21.76);
  let lon = $state<number>(8.53);
  let zoom = $state<number>(1.1);
  // Where the map is, for the URL: the start's view until the map reports its own.
  let view = { zoom: 1.1, lat: 21.76, lon: 8.53 };
  // Types in the toggles' order, so switching one off and on again gives back the same filter.
  let filter = $state<string>("type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=1d");
  let importOpen = $state(false);
  let dataVersion = $state(0);
  let network = $state<Record<string, unknown>>();
  let filters = $state<Filters>();
  let timeFilterHeight = $state(0);
  // The short form of the filter in the top row, for the details panel to start below it.
  let topControlsHeight = $state(0);

  // New entries go here; `visible` decides when each one shows.
  const menuItems: MenuItem[] = [
    {
      id: "sign-in",
      label: "Sign in",
      icon: signInIcon,
      visible: () => !auth.signedIn,
      action: () => auth.openSignIn(),
    },
    {
      id: "import-files",
      label: "Import files",
      icon: uploadIcon,
      visible: () => auth.signedIn,
      action: () => (importOpen = true),
    },
    {
      id: "sign-out",
      label: "Sign out",
      icon: signOutIcon,
      visible: () => auth.signedIn,
      action: () => auth.signOut(),
    },
  ];

  onMount(() => {
    auth.restore();

    // A view or filter in the URL, from a link or a reload, wins. Without one, as when the
    // installed app starts, the last ones come back.
    const searchParams = new URLSearchParams(page.url.searchParams.toString());
    const savedView = loadSavedView();
    if (["lat", "lon", "z"].some((name) => searchParams.has(name))) {
      lat = parseFloat(searchParams.get("lat") ?? "21.76");
      lon = parseFloat(searchParams.get("lon") ?? "8.53");
      zoom = parseFloat(searchParams.get("z") ?? "1.1");
    } else if (savedView) {
      lat = savedView.lat;
      lon = savedView.lon;
      zoom = savedView.zoom;
    }
    view = { zoom, lat, lon };

    searchParams.delete("lat");
    searchParams.delete("lon");
    searchParams.delete("z");
    // Decoded, `+` included: the filter's values are plain text.
    const urlFilter = Array.from(searchParams, ([name, value]) => `${name}=${value}`).join("&");

    if (urlFilter != "") filter = urlFilter;
    else filter = loadSavedFilter() ?? filter;
  });

  // The first run comes after onMount has picked the view and the filter.
  $effect(() => {
    saveFilter(filter);
    writeUrl();
  });

  $effect(() => {
    if (!auth.signedIn) importOpen = false;
  });

  /**
   * Writes the view and the filter into the URL, the whole of it from the page's state. Built on the
   * URL instead, a write would lose what an earlier one had added: `goto` changes the URL only when
   * its navigation is done, and a newer navigation cancels the one still on its way. At the start,
   * the map reporting its view would drop the filter that way.
   */
  function writeUrl() {
    if (typeof window === "undefined") return; // Skip on server

    const params = new URLSearchParams();
    params.set("z", view.zoom.toFixed(1));
    params.set("lat", view.lat.toFixed(7));
    params.set("lon", view.lon.toFixed(7));
    // The filter replaces everything else, so a parameter taken out of the filter (the slider
    // drops `time[lt]` for "Last 24 hours") leaves the URL as well.
    for (const [name, value] of filterParams(filter)) params.set(name, value);

    goto(`${window.location.pathname}?${params.toString()}`, { replaceState: false });
  }

  function onChangeView(zoom: number, lat: number, lon: number) {
    view = { zoom, lat, lon };
    saveView(view);
    writeUrl();
  }
</script>

{#snippet signedInAs()}
  <div class="flex items-center gap-3">
    <Icon svg={userIcon} class="size-5 text-gray-400" />
    <span class="truncate text-sm font-medium text-gray-900">
      <span class="sr-only">Signed in as</span>
      {auth.userName}
    </span>
  </div>
{/snippet}

<div class="app">
  <div
    class="map-area"
    style:--time-filter-height="{auth.signedIn ? timeFilterHeight : 0}px"
    style:--top-controls-height="{topControlsHeight}px"
  >
    {#if lat && lon}
      <MapLibre
        {filter}
        {onChangeView}
        {zoom}
        {lat}
        lng={lon}
        signedIn={auth.signedIn}
        {dataVersion}
        bind:network
      >
        {#snippet controls()}
          {#if auth.signedIn}
            <Filters bind:this={filters} bind:filter />
          {/if}
        {/snippet}
      </MapLibre>
    {/if}
    <div class="maplibregl-control-container">
      {#if network}
        <NetworkPanel properties={network} onclose={() => (network = undefined)} />
      {/if}
      <div
        class={["top-controls maplibregl-ctrl-top-left", network && "max-sm:invisible"]}
        bind:clientHeight={topControlsHeight}
      >
        {#if auth.signedIn}
          <FilterSummary {filter} onopen={(selector) => filters?.show(selector)} />
        {/if}
      </div>
      <div class="maplibregl-ctrl-top-right">
        <AppMenu
          items={menuItems}
          header={auth.signedIn ? signedInAs : undefined}
        />
      </div>
    </div>
    {#if auth.signedIn}
      <TimeFilter bind:filter bind:height={timeFilterHeight} />
    {/if}
  </div>
  <Login />
  {#if auth.signedIn && importOpen}
    <ImportFiles
      onclose={() => (importOpen = false)}
      onimport={() => (dataVersion += 1)}
    />
  {/if}
</div>

<style>
  /* Holds the floated controls, so that its height is theirs. */
  .top-controls {
    display: flow-root;
  }

  .map-area {
    position: relative;
    height: 100dvh;
  }

  /*
   * The round buttons over the map: the menu button, the funnel (both `map-button`) and MapLibre's
   * location, zoom and compass buttons. Slightly see-through white, a 1px gray edge, 36px across.
   * MapLibre.svelte joins the zoom buttons and draws the icons.
   *
   * The corner classes make these beat MapLibre's own rules, which go up to three classes and an
   * element for the hover color and the focus corners.
   */
  .map-area
    :global(:is(.maplibregl-ctrl-top-right, .maplibregl-ctrl-bottom-right) :is(.map-button, .maplibregl-ctrl-group button)) {
    width: 36px;
    height: 36px;
    padding: 0;
    border: 1px solid var(--color-gray-300);
    border-radius: 50%;
    background: rgb(255 255 255 / 0.8);
    color: var(--color-gray-600);
    box-shadow: none;
    -webkit-tap-highlight-color: transparent;
  }

  .map-area :global(:is(.maplibregl-ctrl-top-right, .maplibregl-ctrl-bottom-right) .map-button) {
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
  }

  .map-area
    :global(
      :is(.maplibregl-ctrl-top-right, .maplibregl-ctrl-bottom-right)
        :is(.map-button, .maplibregl-ctrl-group button):not(:disabled, .active, .maplibregl-ctrl-geolocate-active):hover
    ) {
    background: color-mix(in srgb, var(--color-gray-100) 90%, transparent);
  }

  /* Switched on: the funnel while the filter limits the map, the location button while the map
     follows the location. */
  .map-area
    :global(
      :is(.maplibregl-ctrl-top-right, .maplibregl-ctrl-bottom-right)
        :is(.map-button.active, .maplibregl-ctrl-group .maplibregl-ctrl-geolocate-active)
    ) {
    color: var(--color-indigo-600);
    background: color-mix(in srgb, var(--color-indigo-600) 14%, rgb(255 255 255 / 0.8));
    border-color: color-mix(in srgb, var(--color-indigo-600) 45%, white);
  }

  .map-area
    :global(
      :is(.maplibregl-ctrl-top-right, .maplibregl-ctrl-bottom-right)
        :is(.map-button, .maplibregl-ctrl-group button):focus-visible
    ) {
    outline: 2px solid var(--color-indigo-600);
    outline-offset: 1px;
  }

  /* The time range bar lies over the bottom of the map; MapLibre's corner controls move up. */
  .map-area :global(.maplibregl-ctrl-bottom-left),
  .map-area :global(.maplibregl-ctrl-bottom-right) {
    bottom: var(--time-filter-height, 0px);
  }

  :global(body) {
    margin: 0;
  }
</style>
