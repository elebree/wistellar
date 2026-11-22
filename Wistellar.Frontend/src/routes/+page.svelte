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
  // Types in the toggles' order, so switching one off and on again gives back the same filter.
  let filter = $state<string>("type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=1d");
  let importOpen = $state(false);
  let dataVersion = $state(0);
  let network = $state<Record<string, unknown>>();
  let timeFilterHeight = $state(0);
  // The filter box and the rows under it, for the details panel to start below them.
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

    searchParams.delete("lat");
    searchParams.delete("lon");
    searchParams.delete("z");
    // Decoded, `+` included: the filter's values are plain text.
    const urlFilter = Array.from(searchParams, ([name, value]) => `${name}=${value}`).join("&");

    if (urlFilter != "") filter = urlFilter;
    else filter = loadSavedFilter() ?? filter;
  });

  $effect(() => {
    updateQueryStringFromFilter(filter);
    saveFilter(filter);
  });

  $effect(() => {
    if (!auth.signedIn) importOpen = false;
  });

  function updateQueryStringFromFilter(filter: string) {
    if (typeof window === "undefined") return; // Skip on server

    // The view stays, and the filter replaces everything else, so a parameter taken out of the
    // filter (the slider drops `time[lt]` for "Last 24 hours") leaves the URL as well.
    const current = new URLSearchParams(window.location.search);
    const params = new URLSearchParams();
    for (const name of ["z", "lat", "lon"]) {
      const value = current.get(name);
      if (value !== null) params.set(name, value);
    }
    for (const [name, value] of filterParams(filter)) params.set(name, value);

    const newUrl = `${window.location.pathname}?${params.toString()}`;
    goto(newUrl, { replaceState: false });
  }

  function updateQueryStringFromPosition(
    zoom: number,
    lat: number,
    lon: number
  ) {
    if (typeof window === "undefined") return; // Skip on server

    const params = new URLSearchParams(window.location.search);
    params.set("z", zoom.toFixed(1));
    params.set("lat", lat.toFixed(7));
    params.set("lon", lon.toFixed(7));

    const newUrl = `${window.location.pathname}?${params.toString()}`;
    goto(newUrl, { replaceState: false });
  }

  function onChangeView(zoom: number, lat: number, lng: number) {
    saveView({ lat, lon: lng, zoom });
    updateQueryStringFromPosition(zoom, lat, lng);
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
      />
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
          <input
            type="text"
            name="filter"
            id="filter"
            class="filter-box maplibregl-ctrl block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
            placeholder="Filter query"
            value={filter}
            onkeydown={(e) => {
              if (e.key === "Enter") {
                const el = e?.target as any;
                filter = el.value;
                el.blur();
              }
            }}
            onblur={(e) => {
              filter = (e?.target as any).value;
            }}
          />
          <div class="maplibregl-ctrl relative flex max-w-[calc(100vw-20px)] flex-wrap items-start gap-2">
            <Filters bind:filter />
          </div>
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
  .filter-box {
    border-radius: 50vh;
    /* 10px margin on each side, plus the 36px menu button and a 10px gap before it. */
    width: calc(100vw - 66px);
  }

  /* Holds the floated controls, so that its height is theirs. */
  .top-controls {
    display: flow-root;
  }

  .map-area {
    position: relative;
    height: 100dvh;
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
