<script lang="ts">
  import { TimeRangeSlider, labelsEn, type TimeRangeChange } from "svelte-time-range";
  import { MediaQuery } from "svelte/reactivity";
  import { filterWithTimeRange, timeParamsOf, timeRangeFromFilter } from "#lib/time-filter.ts";
  import { readCookie, timeRulerCookie, writeCookie } from "#lib/ui-cookies.ts";

  interface Props {
    /** The filter query; the slider reads and writes its `time[gt]` and `time[lt]`. */
    filter: string;
    /** The bar's height in px, for the page to keep the map's own controls above it. */
    height?: number;
    openHeight?: number;
    closedHeight?: number;
  }

  let {
    filter = $bindable(),
    height = $bindable(0),
    openHeight = $bindable(0),
    closedHeight = $bindable(0),
  }: Props = $props();

  // Shown until the filter has a range of its own: the last day.
  const now = Date.now();
  let to = $state(new Date(now));
  let from = $state(new Date(now - 24 * 60 * 60 * 1000));

  // The time parameters this slider wrote last. Reading them back would move the slider, e.g.
  // "This month" to "Nov 1 – now", so only a change from elsewhere (the filter box, a link) does.
  let written: string | undefined;

  // On phones only the toolbar shows, and the timeline opens on demand as a popover: a tap on the
  // map closes it. Large screens can close it too, but it's open by default and stays as it was
  // left, across launches.
  const narrow = new MediaQuery("max-width: 639px");
  let popoverOpen = $state(false);
  let rulerOpen = $state(readCookie(timeRulerCookie) !== "closed");
  const expanded = {
    get: () => (narrow.current ? popoverOpen : rulerOpen),
    set(open: boolean) {
      if (narrow.current) {
        popoverOpen = open;
      } else {
        rulerOpen = open;
        writeCookie(timeRulerCookie, open ? "open" : "closed");
      }
    },
  };

  $effect(() => {
    if (timeParamsOf(filter) === written) return;
    written = undefined;
    const range = timeRangeFromFilter(filter, new Date());
    if (range) ({ from, to } = range);
  });

  // The border box, unlike offsetHeight, keeps fractions of a pixel.
  let boxSize = $state<ResizeObserverSize[]>();
  let bar: HTMLDivElement;
  let rulerExtent = $state(72);
  let rulerShown = $state(false);
  $effect(() => {
    const card = bar.querySelector(".trs");
    const ruler = bar.querySelector(".trs-ruler");
    if (!card || !ruler) return;
    const observer = new ResizeObserver(([entry]) => {
      const rulerHeight = entry.borderBoxSize[0].blockSize;
      rulerShown = rulerHeight > 0;
      if (rulerShown) rulerExtent = rulerHeight + parseFloat(getComputedStyle(card).rowGap);
    });
    observer.observe(ruler);
    return () => observer.disconnect();
  });

  $effect(() => {
    height = boxSize?.[0]?.blockSize ?? 0;
    openHeight = rulerShown ? height : height + rulerExtent;
    closedHeight = rulerShown ? height - rulerExtent : height;
  });

  function onchange(change: TimeRangeChange) {
    filter = filterWithTimeRange(filter, change.from, change.to, new Date());
    written = timeParamsOf(filter);
  }
</script>

<div class="time-filter" bind:this={bar} bind:borderBoxSize={boxSize}>
  <!-- The app is in English; dates and the first day of the week still follow the browser's locale. -->
  <TimeRangeSlider
    bind:from
    bind:to
    bind:expanded={expanded.get, expanded.set}
    labels={labelsEn}
    ruler={narrow.current ? "popover" : "toggle"}
    {onchange}
  />
</div>

<style>
  /* Over the bottom of the map. The bar itself is transparent; only the slider shows, frosted. */
  .time-filter {
    position: absolute;
    inset-inline: 0;
    bottom: 0;
    z-index: 2;
    padding: 0.375rem 0.625rem calc(0.375rem + env(safe-area-inset-bottom, 0px));
    /* No grey flash on a tap. */
    -webkit-tap-highlight-color: transparent;
    --trs-accent: var(--color-indigo-600);
    --trs-selection: color-mix(in oklch, var(--color-indigo-600) 15%, transparent);
    /* Buttons, the period group and the dropdown are a lighter layer of the same glass. */
    --trs-surface: rgb(255 255 255 / 0.5);
    --trs-track-background: rgb(255 255 255 / 0.4);
    --trs-border: rgb(15 23 42 / 0.12);
  }

  /* The same frosted glass as the network details panel. Listed twice because the slider's
     own `.trs[data-collapsible]` card is as specific as `.time-filter .trs`, and comes later. */
  .time-filter :global(.trs),
  .time-filter :global(.trs[data-collapsible]) {
    padding: 8px;
    border: 1px solid rgb(255 255 255 / 0.6);
    border-radius: 12px;
    background: var(--glass-background);
    backdrop-filter: var(--glass-filter);
    box-shadow:
      0 0 0 1px rgb(15 23 42 / 0.06),
      0 8px 24px rgb(15 23 42 / 0.12);
    /* The slider mixes its hover colour into the surface, which would be solid grey on glass. */
    --_hover: rgb(15 23 42 / 0.06);
  }

  /* The presets dialog floats over the map in the top layer: the same glass as the card. */
  .time-filter :global(.trs-periods) {
    border: 1px solid rgb(255 255 255 / 0.6);
    background: var(--glass-background);
    backdrop-filter: var(--glass-filter);
    box-shadow:
      0 0 0 1px rgb(15 23 42 / 0.06),
      0 8px 24px rgb(15 23 42 / 0.12);
  }

  /* On phones the card is a sheet docked to the bottom edge, full width, so the ruler gets every
     pixel: only a top border and rounded top corners, the safe areas inside its padding. Matches
     the narrow MediaQuery in the script. */
  @media (max-width: 639px) {
    .time-filter {
      padding: 0;
    }

    .time-filter :global(.trs),
    .time-filter :global(.trs[data-collapsible]) {
      padding: 8px max(8px, env(safe-area-inset-right, 0px)) calc(8px + env(safe-area-inset-bottom, 0px))
        max(8px, env(safe-area-inset-left, 0px));
      border-width: 1px 0 0;
      border-radius: 12px 12px 0 0;
    }
  }

  /* A touch keeps :hover until the next one, which left the tapped date text grey. */
  @media (hover: none) {
    .time-filter :global(.trs),
    .time-filter :global(.trs[data-collapsible]) {
      --_hover: transparent;
    }
  }

  /* What floats over the ruler stays solid. */
  .time-filter :global(.trs-knob),
  .time-filter :global(.trs-tooltip) {
    background: white;
  }
</style>
