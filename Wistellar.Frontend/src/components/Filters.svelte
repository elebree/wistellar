<script lang="ts">
  import { tick } from "svelte";
  import Icon from "./Icon.svelte";
  import TypeToggles from "./TypeToggles.svelte";
  import RangeFilterSlider from "./RangeFilterSlider.svelte";
  import PatternField from "./PatternField.svelte";
  import QueryField from "./QueryField.svelte";
  import funnelIcon from "lucide-static/icons/funnel.svg?raw";
  import radioIcon from "lucide-static/icons/radio.svg?raw";
  import { filterWithAllTypes, typesLimited } from "#lib/network-types.ts";
  import { boundedRanges, filterWithoutRanges, rangeFilters } from "#lib/range-filters.ts";
  import { filterWithoutPatterns, limitedPatterns, patternFilters, patternSyntax } from "#lib/pattern-filters.ts";

  interface Props {
    /**
     * The filter query; the pane reads and writes its `type`, its `ssid` and `bssid`, and its range, dwell and
     * observation bounds, and has a field for the whole query.
     */
    filter: string;
  }

  let { filter = $bindable() }: Props = $props();

  const id = $props.id();
  const title = "Filters";
  let open = $state(false);
  let root: HTMLElement;
  let button: HTMLButtonElement;
  let pane = $state<HTMLElement>();

  // Tinted while something limits the map; the short form in the top row says what.
  let active = $derived(
    typesLimited(filter) || limitedPatterns(filter).length > 0 || boundedRanges(filter, new Date()).length > 0,
  );

  /** Opens the pane with the focus on a control in it: the first one, or the one `selector` finds. */
  export async function show(selector = "button, input") {
    open = true;
    await tick();
    pane?.querySelector<HTMLElement>(selector)?.focus();
  }

  function hide(returnFocus: boolean) {
    open = false;
    if (returnFocus) button.focus();
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === "Escape" && open) {
      e.preventDefault();
      hide(true);
    }
  }

  // The short form's buttons open the pane too, at their control, so they don't count as outside.
  function onWindowPointerDown(e: PointerEvent) {
    const target = e.target as Element;
    if (open && !root.contains(target) && !target.closest?.("[data-opens-filters]")) hide(false);
  }

  function reset() {
    filter = filterWithoutPatterns(filterWithoutRanges(filterWithAllTypes(filter)));
  }
</script>

<!-- Capture phase, so the map's own pointer handling can't swallow the event. -->
<svelte:window onpointerdowncapture={onWindowPointerDown} />

<!--
  The funnel sits in the column down the map's right edge, which gives it the look of the map's own
  buttons (`map-button`). The pane opens to the left of that column.
-->
<div class="filters" bind:this={root}>
  <button
    bind:this={button}
    type="button"
    class="map-button funnel"
    class:active
    aria-label={title}
    title={title}
    aria-expanded={open}
    aria-controls={open ? `${id}-pane` : undefined}
    onclick={() => (open ? hide(false) : show())}
    onkeydown={onKeydown}
  >
    <Icon svg={funnelIcon} class="size-5" />
  </button>

  {#if open}
    <div
      bind:this={pane}
      id="{id}-pane"
      role="dialog"
      aria-label={title}
      tabindex="-1"
      class="pane z-1 flex w-80 flex-col gap-3 overflow-y-auto rounded-lg glass p-3 shadow-lg ring-1 ring-black/5"
      onkeydown={onKeydown}
    >
      <div>
        <div class="mb-1.5 flex items-center gap-2 text-sm">
          <Icon svg={radioIcon} class="size-5 text-gray-500" />
          <span class="font-medium text-gray-900">Network types</span>
        </div>
        <TypeToggles bind:filter />
      </div>
      {#each patternFilters as pattern (pattern.name)}
        <PatternField bind:filter {pattern} syntaxId="{id}-syntax" />
      {/each}
      <p id="{id}-syntax" class="-mt-1.5 text-xs text-gray-500">{patternSyntax}</p>
      {#each rangeFilters as range (range.name)}
        <RangeFilterSlider bind:filter {range} />
      {/each}
      <QueryField bind:filter />
      {#if active}
        <button
          type="button"
          class="self-end rounded-md px-2 py-1 text-sm font-medium text-indigo-600 hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
          onclick={reset}
        >
          Reset
        </button>
      {/if}
    </div>
  {/if}
</div>

<style>
  .filters {
    display: contents;
  }

  /*
   * Left of the button column (10px margin, the 36px buttons, a 10px gap), from the bottom of the
   * corner up to below the top controls. Placed against the window rather than the funnel: the
   * map's corner grows wide with the attribution, and the funnel is too high up for the pane to
   * fit above it.
   */
  .pane {
    position: fixed;
    right: 56px;
    bottom: calc(var(--time-filter-height, 0px) + 10px);
    max-width: calc(100vw - 66px);
    max-height: calc(100dvh - var(--top-controls-height, 46px) - var(--time-filter-height, 0px) - 20px);
  }
</style>
