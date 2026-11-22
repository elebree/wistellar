<script lang="ts">
  import Icon from "./Icon.svelte";
  import {
    boundAt,
    boundsOf,
    clampHandle,
    describeBounds,
    describeHandle,
    filterWithHandle,
    handlesFor,
    type RangeFilter,
  } from "#lib/range-filters.ts";

  interface Props {
    /** The filter query; the slider reads and writes the range's `[gt]` and `[lt]`. */
    filter: string;
    range: RangeFilter;
  }

  let { filter = $bindable(), range }: Props = $props();

  const id = $props.id();
  const last = $derived(range.stops.length - 1);

  let bounds = $derived(boundsOf(filter, range, new Date()));
  let snapped = $derived(handlesFor(range, bounds));

  // The handles follow a drag; the filter changes once one is let go (or moved with a key).
  let lo = $state(0);
  let hi = $state(0);
  $effect(() => {
    ({ lo, hi } = snapped);
  });

  // While a handle is where the filter put it, the filter's own value shows, which may lie
  // between the stops (typed into the filter box). Moved, it shows its stop.
  let min = $derived(lo === snapped.lo ? bounds.min : boundAt(range, "min", lo));
  let max = $derived(hi === snapped.hi ? bounds.max : boundAt(range, "max", hi));

  // Where the handles meet, the one on top is the one that can still move away from there: the
  // lower one in the right half, the upper one in the left half.
  let minOnTop = $derived(lo === hi && lo > last / 2);

  function moveLo(input: HTMLInputElement) {
    lo = clampHandle(range, "min", Number(input.value), hi);
    input.value = String(lo);
  }

  function moveHi(input: HTMLInputElement) {
    hi = clampHandle(range, "max", Number(input.value), lo);
    input.value = String(hi);
  }
</script>

<div class="range-slider" role="group" aria-labelledby="{id}-label" aria-describedby="{id}-hint" data-range={range.name}>
  <div class="flex items-center gap-2 text-sm">
    <Icon svg={range.icon} class="size-5 text-gray-500" />
    <span id="{id}-label" class="font-medium text-gray-900">{range.label}</span>
    <span class="ml-auto text-gray-600" data-testid="range-summary">{describeBounds(range, { min, max })}</span>
  </div>
  <p id="{id}-hint" class="mt-0.5 pl-7 text-xs text-gray-500">{range.hint}</p>
  <div class="track-area" style:--lo={lo / last} style:--hi={hi / last}>
    <div class="track"><div class="fill"></div></div>
    <input
      type="range"
      class:on-top={minOnTop}
      min="0"
      max={last}
      step="1"
      value={lo}
      aria-label="Minimum {range.noun}"
      aria-valuetext={describeHandle(range, "min", min)}
      oninput={(e) => moveLo(e.currentTarget)}
      onchange={(e) => {
        moveLo(e.currentTarget);
        filter = filterWithHandle(filter, range, "min", lo);
      }}
    />
    <input
      type="range"
      min="0"
      max={last}
      step="1"
      value={hi}
      aria-label="Maximum {range.noun}"
      aria-valuetext={describeHandle(range, "max", max)}
      oninput={(e) => moveHi(e.currentTarget)}
      onchange={(e) => {
        moveHi(e.currentTarget);
        filter = filterWithHandle(filter, range, "max", hi);
      }}
    />
  </div>
</div>

<style>
  /* Two range inputs over one track: each draws only its handle, and only the handles take the
     pointer, so either can be dragged where they meet. 16px handles, so the track is inset 8px. */
  .track-area {
    position: relative;
    height: 24px;
    margin-top: 4px;
  }

  .track {
    position: absolute;
    inset-inline: 8px;
    top: 10px;
    height: 4px;
    border-radius: 2px;
    background: var(--color-gray-200);
  }

  .fill {
    position: absolute;
    inset-block: 0;
    left: calc(var(--lo) * 100%);
    right: calc((1 - var(--hi)) * 100%);
    border-radius: 2px;
    background: var(--color-indigo-600);
  }

  input {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 24px;
    margin: 0;
    background: none;
    appearance: none;
    pointer-events: none;
    -webkit-tap-highlight-color: transparent;
  }

  input:focus {
    outline: none;
  }

  input.on-top {
    z-index: 1;
  }

  input::-webkit-slider-runnable-track {
    height: 24px;
    background: none;
  }

  input::-moz-range-track {
    background: none;
  }

  input::-webkit-slider-thumb {
    width: 16px;
    height: 16px;
    margin-top: 4px;
    border: 2px solid var(--color-indigo-600);
    border-radius: 50%;
    background: white;
    box-shadow: 0 1px 3px rgb(15 23 42 / 0.2);
    appearance: none;
    pointer-events: auto;
    cursor: grab;
  }

  input::-moz-range-thumb {
    width: 12px;
    height: 12px;
    border: 2px solid var(--color-indigo-600);
    border-radius: 50%;
    background: white;
    box-shadow: 0 1px 3px rgb(15 23 42 / 0.2);
    pointer-events: auto;
    cursor: grab;
  }

  input:focus-visible::-webkit-slider-thumb {
    box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-indigo-600) 35%, transparent);
  }

  input:focus-visible::-moz-range-thumb {
    box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-indigo-600) 35%, transparent);
  }
</style>
