<script lang="ts">
  interface Props {
    /** SVG markup, imported with `?raw` from `src/lib/icons/`. Use `currentColor` so it follows the text. */
    svg: string;
    /** Size and color, e.g. `size-5 text-gray-400`. */
    class?: string;
  }

  let { svg, class: className = "" }: Props = $props();
</script>

<!-- Decorative: the text next to an icon carries the meaning. -->
<span class="icon {className}" aria-hidden="true">{@html svg}</span>

<style>
  .icon {
    display: inline-block;
    flex-shrink: 0;
  }

  /* The SVG comes in through {@html}, so it needs :global. The span sets the size. */
  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }
</style>
