<script lang="ts">
  import Icon from "./Icon.svelte";
  import { filterWithTypeToggled, networkTypes, shownTypes, typeColor } from "#lib/network-types.ts";

  interface Props {
    /** The filter query; the toggles read and write its `type`. */
    filter: string;
  }

  let { filter = $bindable() }: Props = $props();

  let shown = $derived(shownTypes(filter));
</script>

<!-- A pressed toggle has its layer's color on the map, so the row doubles as the map's legend. -->
<div class="type-toggles" role="group" aria-label="Network types">
  {#each networkTypes as type (type.code)}
    {@const pressed = shown.includes(type.code)}
    <!-- Switching off the only type left would show every type, so that one stays on. -->
    {@const last = pressed && shown.length === 1}
    <button
      type="button"
      class="type-badge"
      aria-label={type.badge}
      title={type.label}
      aria-pressed={pressed}
      aria-disabled={last || undefined}
      style:--type-color={typeColor(type)}
      onclick={() => {
        if (!last) filter = filterWithTypeToggled(filter, type.code);
      }}
    >
      <Icon svg={type.icon} class="size-8" />
    </button>
  {/each}
</div>

<style>
  /* White with the filter box's 1px outline, 36px high like it. The corners follow the badges'. */
  .type-toggles {
    display: flex;
    width: max-content;
    align-items: center;
    gap: 2px;
    padding: 2px;
    border-radius: calc(0.25rem + 2px);
    background: #fff;
    outline: 1px solid var(--color-gray-300);
    outline-offset: -1px;
    -webkit-tap-highlight-color: transparent;
  }

  .type-toggles button {
    cursor: pointer;
  }

  /* Off: a grey outline only. Qualified with the row so that it beats MapLibre's
     `.maplibregl-ctrl button:not(:disabled):hover`, which only the off state keeps. */
  .type-toggles button[aria-pressed="false"] {
    color: var(--color-gray-500);
    background-color: transparent;
    box-shadow: inset 0 0 0 1px var(--color-gray-200);
  }

  .type-toggles button[aria-pressed="false"]:hover {
    background-color: var(--color-gray-100);
  }

  .type-toggles button[aria-pressed="true"]:hover {
    background-color: color-mix(in srgb, var(--type-color) 24%, transparent);
  }

  .type-toggles button[aria-disabled="true"] {
    cursor: default;
  }

  .type-toggles button:focus-visible {
    outline: 2px solid var(--color-indigo-600);
    outline-offset: 1px;
  }
</style>
