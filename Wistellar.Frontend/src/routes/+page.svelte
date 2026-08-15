<script lang="ts">
  import "../app.css";
  import { onMount } from "svelte";
  import MapLibre from "../components/MapLibre.svelte";
  import Login from "../components/Login.svelte";
  import authService from "$lib/authService";
  import { page } from "$app/state";
  import { goto } from "$app/navigation";

  let lat = $state<number>(21.76);
  let lon = $state<number>(8.53);
  let zoom = $state<number>(1.1);
  let filter = $state<string>("type=W|E|B|G|L|C|D|N&ssid=_%&time[gt]=1d");

  onMount(() => {
    const searchParams = page.url.searchParams;
    lat = parseFloat(searchParams.get("lat") ?? "21.76");
    lon = parseFloat(searchParams.get("lon") ?? "8.53");
    zoom = parseFloat(searchParams.get("z") ?? "1.1");

    searchParams.delete("lat");
    searchParams.delete("lon");
    searchParams.delete("z");
    const urlFilter = decodeURIComponent(searchParams.toString());

    if (urlFilter != "") filter = urlFilter;
  });

  let loggedIn = $state(authService.isAuthorized());

  $effect(() => {
    updateQueryStringFromFilter(filter);
  });

  function updateQueryStringFromFilter(filter: string) {
    if (typeof window === "undefined") return; // Skip on server

    const filterParams = new URLSearchParams("?" + filter);

    const params = new URLSearchParams(window.location.search);
    filterParams
      .keys()
      .forEach((k) => params.set(k, filterParams.get(k) ?? ""));

    const newUrl = `${window.location.pathname}?${params.toString()}`;
    goto(newUrl, { replaceState: false });
  }

  function onMapError(ev: ErrorEvent) {
    // A 401 on a tile request means the stored token expired; drop back to the login form.
    if (ev.error.status === 401) {
      authService.logout();
      loggedIn = authService.isAuthorized();
    } else {
      console.error(ev);
    }
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
    updateQueryStringFromPosition(zoom, lat, lng);
  }
</script>

<div class="app">
  {#if loggedIn === false}
    <Login
      loggedIn={() => {
        loggedIn = authService.isAuthorized();
      }}
    />
  {/if}
  {#if loggedIn === true && filter && lat && lon}
    <MapLibre
      {filter}
      {onChangeView}
      {zoom}
      {lat}
      lng={lon}
      onerror={onMapError}
    />
  {/if}
  {#if loggedIn === true}
    <div class="maplibregl-control-container">
      <div class=" maplibregl-ctrl-top-left">
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
      </div>
    </div>
  {/if}
</div>

<style>
  .filter-box {
    border-radius: 50vh;
    width: calc(100vw - 20px);
  }

  :global(body) {
    margin: 0;
  }
</style>
