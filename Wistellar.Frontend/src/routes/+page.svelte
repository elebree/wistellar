<script lang="ts">
  import "../app.css";
  import { onMount } from "svelte";
  import MapLibre from "../components/MapLibre.svelte";
  import Login from "../components/Login.svelte";
  import AppMenu, { type MenuItem } from "../components/AppMenu.svelte";
  import { auth } from "#lib/auth.svelte.ts";
  import signInIcon from "#lib/icons/sign-in.svg?raw";
  import signOutIcon from "#lib/icons/sign-out.svg?raw";
  import userIcon from "#lib/icons/user.svg?raw";
  import Icon from "../components/Icon.svelte";
  import { page } from "$app/state";
  import { goto } from "$app/navigation";

  let lat = $state<number>(21.76);
  let lon = $state<number>(8.53);
  let zoom = $state<number>(1.1);
  let filter = $state<string>("type=W|E|B|G|L|C|D|N&ssid=_%&time[gt]=1d");

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
      id: "sign-out",
      label: "Sign out",
      icon: signOutIcon,
      visible: () => auth.signedIn,
      action: () => auth.signOut(),
    },
  ];

  onMount(() => {
    auth.restore();

    const searchParams = new URLSearchParams(page.url.searchParams.toString());
    lat = parseFloat(searchParams.get("lat") ?? "21.76");
    lon = parseFloat(searchParams.get("lon") ?? "8.53");
    zoom = parseFloat(searchParams.get("z") ?? "1.1");

    searchParams.delete("lat");
    searchParams.delete("lon");
    searchParams.delete("z");
    const urlFilter = decodeURIComponent(searchParams.toString());

    if (urlFilter != "") filter = urlFilter;
  });

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
    // A 401 on a tile request means the stored token expired or is no longer accepted.
    if (ev.error?.status === 401) {
      auth.expire();
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
  {#if lat && lon}
    <MapLibre
      {filter}
      {onChangeView}
      {zoom}
      {lat}
      lng={lon}
      signedIn={auth.signedIn}
      onerror={onMapError}
    />
  {/if}
  <div class="maplibregl-control-container">
    <div class=" maplibregl-ctrl-top-left">
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
      {/if}
    </div>
    <div class="maplibregl-ctrl-top-right">
      <AppMenu
        items={menuItems}
        header={auth.signedIn ? signedInAs : undefined}
      />
    </div>
  </div>
  <Login />
</div>

<style>
  .filter-box {
    border-radius: 50vh;
    /* 10px margin on each side, plus the 36px menu button and a 10px gap before it. */
    width: calc(100vw - 66px);
  }

  :global(body) {
    margin: 0;
  }
</style>
