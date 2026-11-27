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
  .type-toggles {
    display: flex;
    width: max-content;
    align-items: center;
    gap: 2px;
    -webkit-tap-highlight-color: transparent;
  }

  .type-toggles button {
    width: 2rem;
    height: 2rem;
    border-radius: 0.25rem;
    cursor: pointer;
  }

  .type-toggles button:not(:disabled):hover {
    background-color: transparent;
  }

  .type-toggles button[aria-pressed="false"] {
    color: var(--color-gray-400);
  }

  .type-toggles button[aria-pressed="false"]:hover {
    color: var(--color-gray-500);
  }

  .type-toggles button[aria-disabled="true"] {
    cursor: default;
  }

  .type-toggles button:focus-visible {
    outline: 2px solid var(--color-indigo-600);
    outline-offset: 1px;
  }
</style>
