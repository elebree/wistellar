<script lang="ts">
  import Icon from "./Icon.svelte";
  import { networkTypeOf, shownTypes, typeColor, typesLimited } from "#lib/network-types.ts";
  import { boundedRanges, describeBounds } from "#lib/range-filters.ts";
  import { limitedPatterns } from "#lib/pattern-filters.ts";

  interface Props {
    filter: string;
    /** Opens the filters pane with the focus on the control `selector` finds. */
    onopen: (selector: string) => void;
  }

  let { filter, onopen }: Props = $props();

  // Only what limits the map.
  let limitedTypes = $derived(typesLimited(filter) ? shownTypes(filter).map((code) => networkTypeOf(code)) : []);
  let patterns = $derived(limitedPatterns(filter));
  let bounded = $derived(boundedRanges(filter, new Date()));
  let any = $derived(limitedTypes.length > 0 || patterns.length > 0 || bounded.length > 0);
</script>

<!--
  The short form of the filters pane, in the top row left of the menu button, wrapping onto more
  lines where it runs out of room. Each button opens the pane at its control; `data-opens-filters`
  keeps the pane from taking a press on one for a press outside it. Nothing limits the map, no row.
-->
{#if any}
  <div class="summary-row maplibregl-ctrl" data-opens-filters>
    {#if limitedTypes.length > 0}
      {@const names = limitedTypes.map((type, i) => type?.label ?? shownTypes(filter)[i]).join(", ")}
      <button
        type="button"
        class="summary types"
        aria-label="Network types: {names}"
        title="Network types: {names}"
        onclick={() => onopen(".type-toggles button")}
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
        onclick={() => onopen(`[data-pattern="${pattern.name}"] input`)}
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
        onclick={() => onopen(`[data-range="${range.name}"] input`)}
      >
        <Icon svg={range.icon} class="size-5" />
        {text}
      </button>
    {/each}
  </div>
{/if}

<style>
  .summary-row {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
    gap: 8px;
    /* 10px margin on each side, plus the 36px menu button and a 10px gap before it. */
    max-width: calc(100vw - 66px);
  }

  /* White, the filter box's 1px outline, 36px high, like the map's buttons. Qualified with the row
     so that they beat MapLibre's `.maplibregl-ctrl button:not(:disabled):hover`, which would make
     them see-through. */
  .summary-row .summary {
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

  /* The shown types' badges, as in the type toggles. */
  .summary-row .summary.types {
    gap: 2px;
    padding: 2px;
  }

  .summary-row .summary.range,
  .summary-row .summary.pattern {
    gap: 6px;
    padding-inline: 8px 10px;
    font-size: 0.875rem;
    color: var(--color-gray-900);
    white-space: nowrap;
  }

  .summary-row .summary.range :global(.icon),
  .summary-row .summary.pattern :global(.icon) {
    color: var(--color-indigo-600);
  }

  .summary-row .summary.pattern {
    max-width: 14rem;
  }

  .summary-row .summary.pattern span {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .summary-row .summary:hover {
    background: var(--color-gray-50);
  }

  .summary-row .summary:focus-visible {
    outline: 2px solid var(--color-indigo-600);
    outline-offset: 1px;
  }
</style>
