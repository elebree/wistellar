<script lang="ts">
  import { tick } from "svelte";
  import Icon from "./Icon.svelte";
  import TypeToggles from "./TypeToggles.svelte";
  import RangeFilterSlider from "./RangeFilterSlider.svelte";
  import PatternField from "./PatternField.svelte";
  import funnelIcon from "#lib/icons/funnel.svg?raw";
  import signalIcon from "#lib/icons/signal.svg?raw";
  import { filterWithAllTypes, networkTypeOf, shownTypes, typeColor, typesLimited } from "#lib/network-types.ts";
  import { boundedRanges, describeBounds, filterWithoutRanges, rangeFilters } from "#lib/range-filters.ts";
  import { filterWithoutPatterns, limitedPatterns, patternFilters, patternSyntax } from "#lib/pattern-filters.ts";

  interface Props {
    /**
     * The filter query; the pane reads and writes its `type`, its `ssid` and `bssid`, and its range, dwell and
     * observation bounds.
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

  // The short form, shown next to the button: only what limits the map.
  let limitedTypes = $derived(typesLimited(filter) ? shownTypes(filter).map((code) => networkTypeOf(code)) : []);
  let patterns = $derived(limitedPatterns(filter));
  let bounded = $derived(boundedRanges(filter, new Date()));
  let active = $derived(limitedTypes.length > 0 || patterns.length > 0 || bounded.length > 0);

  /** Opens the pane with the focus on a control in it: the first one, or the one `selector` finds. */
  async function show(selector = "button, input") {
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

  function onWindowPointerDown(e: PointerEvent) {
    if (open && !root.contains(e.target as Node)) hide(false);
  }

  function reset() {
    filter = filterWithoutPatterns(filterWithoutRanges(filterWithAllTypes(filter)));
  }
</script>

<!-- Capture phase, so the map's own pointer handling can't swallow the event. -->
<svelte:window onpointerdowncapture={onWindowPointerDown} />

<!--
  No box of its own: the button and the short form are items of the filter row around it, and wrap
  onto a second line where the row runs out of room. The pane opens under the row.
-->
<div class="filters" bind:this={root}>
  <button
    bind:this={button}
    type="button"
    class="funnel"
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

  {#if limitedTypes.length > 0}
    {@const names = limitedTypes.map((type, i) => type?.label ?? shownTypes(filter)[i]).join(", ")}
    <button
      type="button"
      class="summary types"
      aria-label="Network types: {names}"
      title="Network types: {names}"
      onclick={() => show(".type-toggles button")}
      onkeydown={onKeydown}
    >
      {#each limitedTypes as type, i (i)}
        {#if type}
          <span class="type-badge" style:--type-color={typeColor(type)}>
            <Icon svg={type.icon} class="size-8" />
          </span>
        {/if}
      {/each}
    </button>
  {/if}

  {#each patterns as { pattern, value } (pattern.name)}
    <!-- The pattern as typed, cut short with an ellipsis where it is long. -->
    <button
      type="button"
      class="summary pattern"
      aria-label="{pattern.label}: {value}"
      title="{pattern.label}: {value}"
      onclick={() => show(`[data-pattern="${pattern.name}"] input`)}
      onkeydown={onKeydown}
    >
      <Icon svg={pattern.icon} class="size-5" />
      <span>{value}</span>
    </button>
  {/each}

  {#each bounded as { range, bounds } (range.name)}
    {@const text = describeBounds(range, bounds)}
    <!-- An icon with the numbers: "⏱ over 1 h". -->
    <button
      type="button"
      class="summary range"
      aria-label="{range.label}: {text}"
      title="{range.label}: {text}"
      onclick={() => show(`[data-range="${range.name}"] input`)}
      onkeydown={onKeydown}
    >
      <Icon svg={range.icon} class="size-5" />
      {text}
    </button>
  {/each}

  {#if open}
    <div
      bind:this={pane}
      id="{id}-pane"
      role="dialog"
      aria-label={title}
      tabindex="-1"
      class="absolute top-full left-0 mt-2.5 flex max-h-[calc(100dvh-var(--top-controls-height,92px)-var(--time-filter-height,0px)-20px)] w-80 max-w-[calc(100vw-20px)] flex-col gap-3 overflow-y-auto rounded-lg glass p-3 shadow-lg ring-1 ring-black/5"
      onkeydown={onKeydown}
    >
      <div>
        <div class="mb-1.5 flex items-center gap-2 text-sm">
          <Icon svg={signalIcon} class="size-5 text-gray-500" />
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

  /* The button and the short form: white, the filter box's 1px outline, 36px high. Qualified with
     the root so that they beat MapLibre's `.maplibregl-ctrl button:not(:disabled):hover`, which
     would make them see-through. */
  .filters .funnel,
  .filters .summary {
    display: flex;
    flex-shrink: 0;
    align-items: center;
    height: 36px;
    border-radius: calc(0.25rem + 2px);
    background: #fff;
    outline: 1px solid var(--color-gray-300);
    outline-offset: -1px;
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
  }

  .filters .funnel {
    justify-content: center;
    width: 36px;
    color: var(--color-gray-600);
  }

  /* The shown types' badges, as in the type toggles. */
  .filters .summary.types {
    gap: 2px;
    padding: 2px;
  }

  .filters .summary.range,
  .filters .summary.pattern {
    gap: 6px;
    padding-inline: 8px 10px;
    font-size: 0.875rem;
    color: var(--color-gray-900);
    white-space: nowrap;
  }

  .filters .summary.range :global(.icon),
  .filters .summary.pattern :global(.icon) {
    color: var(--color-indigo-600);
  }

  .filters .summary.pattern {
    max-width: 14rem;
  }

  .filters .summary.pattern span {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .filters .funnel:hover,
  .filters .summary:hover {
    background: var(--color-gray-50);
  }

  .filters .funnel.active {
    color: var(--color-indigo-600);
    background: color-mix(in srgb, var(--color-indigo-600) 12%, white);
    outline-color: color-mix(in srgb, var(--color-indigo-600) 45%, white);
  }

  .filters .funnel:focus-visible,
  .filters .summary:focus-visible {
    outline: 2px solid var(--color-indigo-600);
    outline-offset: 1px;
  }
</style>
