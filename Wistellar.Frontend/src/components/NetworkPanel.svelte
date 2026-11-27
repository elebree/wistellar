<script lang="ts">
  import Icon from "./Icon.svelte";
  import closeIcon from "lucide-static/icons/x.svg?raw";
  import firstSeenIcon from "lucide-static/icons/calendar-plus.svg?raw";
  import lastSeenIcon from "lucide-static/icons/calendar-clock.svg?raw";
  import levelIcon from "lucide-static/icons/signal-high.svg?raw";
  import vendorIcon from "lucide-static/icons/factory.svg?raw";
  import copiedIcon from "lucide-static/icons/check.svg?raw";
  import { networkTypeOf, typeColor } from "#lib/network-types.ts";
  import { patternFilters } from "#lib/pattern-filters.ts";
  import { rangeFilters } from "#lib/range-filters.ts";
  import { formatDistance, formatDuration } from "#lib/format.ts";

  interface Props {
    properties: Record<string, unknown>;
    onclose: () => void;
  }

  let { properties, onclose }: Props = $props();

  let type = $derived(networkTypeOf(properties.type));

  const headerKeys = new Set(["type", "bssid", "ssid", "cap", "lastlat", "lastlon"]);
  const labels: Record<string, string> = {
    firsttime: "First seen",
    lasttime: "Last seen",
    level: "Level",
    // The filter pane's titles, rather than WiGLE's "observations", "range" and "dwell".
    loc: "Locations",
    dist: "Seen across",
    dur: "Seen over",
    vendor: "Vendor",
  };

  // The filter pane's icons and titles where a field is one of its filters, so that the two match.
  const rangeIcon = (name: string) => rangeFilters.find((range) => range.name === name)?.icon;
  const nameField = patternFilters.find((pattern) => pattern.name === "ssid")!;
  const macField = patternFilters.find((pattern) => pattern.name === "bssid")!;
  const icons: Record<string, string | undefined> = {
    firsttime: firstSeenIcon,
    lasttime: lastSeenIcon,
    level: levelIcon,
    loc: rangeIcon("locations"),
    dist: rangeIcon("range"),
    dur: rangeIcon("dwell"),
    vendor: vendorIcon,
  };

  const dateLocale = "de-DE";

  function formatValue(key: string, value: unknown): string {
    switch (key) {
      case "firsttime":
      case "lasttime":
        return new Date(value as number).toLocaleString(dateLocale);
      case "dur": // dwell: seconds between the first and last observation
        return formatDuration(Number(value));
      case "dist": // range: metres between the farthest observations
        return formatDistance(Number(value));
      case "level": // the strongest signal, as the phone measured it
        return `${value} dBm`;
      default:
        return String(value);
    }
  }

  // 0 is a real value (e.g. a range of 0), so only missing ones are skipped.
  const present = (v: unknown) => v !== null && v !== undefined && v !== "";

  // WiFi capabilities look like "[WPA2-PSK-CCMP][ESS]", cell ones are ";"-separated.
  let capabilities = $derived(
    String(properties.cap ?? "")
      .replace(/^\[|\]$/g, "")
      .split("][")
      .join(";")
      .split(";")
      .filter((v) => v !== ""),
  );

  let firstTime = $derived(
    present(properties.lasttime) && present(properties.dur)
      ? Number(properties.lasttime) - Number(properties.dur) * 1000
      : undefined,
  );

  const order = ["firsttime", "lasttime", "dur", "dist", "loc", "level"];
  let details = $derived(
    [...Object.entries(properties), ["firsttime", firstTime] as const]
      .filter(([key, value]) => !headerKeys.has(key) && present(value))
      .map(([key, value]) => ({
        key,
        label: labels[key] ?? key,
        icon: icons[key],
        value: formatValue(key, value),
        paired: order.includes(key),
      }))
      .sort((a, b) => rank(a.key) - rank(b.key)),
  );
  const rank = (key: string) => (order.includes(key) ? order.indexOf(key) : order.length);

  const chip = "inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset break-all";

  /** "Name" to "name" for the middle of a sentence, but "MAC or cell ID" as it is. */
  const lowerFirst = (text: string) => (/^[A-Z][a-z]/.test(text) ? text[0].toLowerCase() + text.slice(1) : text);

  // A chip copies its value; for a moment it then shows a check.
  let copied = $state<string>();
  let copiedTimer: ReturnType<typeof setTimeout> | undefined;
  async function copy(id: string, value: string) {
    try {
      await navigator.clipboard.writeText(value);
    } catch {
      return; // No clipboard here, or it was refused: nothing was copied.
    }
    copied = id;
    clearTimeout(copiedTimer);
    copiedTimer = setTimeout(() => (copied = undefined), 1500);
  }
</script>

{#snippet copyChip(id: string, value: string, colors: string, label: string)}
  <button
    type="button"
    class="{chip} {colors} cursor-pointer text-left hover:brightness-95 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
    title="Copy {lowerFirst(label)}"
    aria-label="Copy {lowerFirst(label)}: {value}"
    onclick={() => copy(id, value)}
  >
    {#if copied === id}
      <Icon svg={copiedIcon} class="size-3.5" />
    {/if}
    {value}
  </button>
{/snippet}

<section
  aria-label="Network details"
  class="absolute inset-x-0 top-0 z-1 max-h-[60dvh] overflow-y-auto glass p-1.5 font-sans text-sm text-gray-700 shadow-lg ring-1 ring-black/5 sm:top-[calc(var(--top-controls-height,92px)+10px)] sm:right-auto sm:left-2.5 sm:max-h-[calc(100%-var(--top-controls-height,92px)-50px-var(--time-filter-height,0px))] sm:w-80 sm:rounded-lg"
>
  <div
    class={[
      "flex items-start gap-2 px-2.5 pt-1",
      details.length > 0 ? "pb-1.5" : "pb-1",
    ]}
  >
    <div class="flex min-w-0 flex-col gap-1.5">
      <!-- Type, name and MAC, in one line where they fit. -->
      <div class="flex flex-wrap items-center gap-1">
        {#if present(properties.type)}
          <!-- The same icon as the type's toggle, as large as the other icons. -->
          {#if type}
            <span class="type-badge" role="img" aria-label={type.label} title={type.label} style:--type-color={typeColor(type)}>
              <Icon svg={type.icon} class="size-5" />
            </span>
          {:else}
            <span class="type-badge size-5">{properties.type}</span>
          {/if}
        {/if}
        {#if present(properties.ssid)}
          {@render copyChip(
            "ssid",
            String(properties.ssid),
            "bg-yellow-50 text-yellow-800 ring-yellow-600/20",
            nameField.label,
          )}
        {/if}
        {#if present(properties.bssid)}
          {@render copyChip(
            "bssid",
            String(properties.bssid),
            "bg-gray-50 text-gray-600 ring-gray-500/10",
            macField.label,
          )}
        {/if}
      </div>
      {#if capabilities.length > 0}
        <div class="flex flex-wrap items-center gap-1">
          {#each capabilities as capability, i (i)}
            {@render copyChip(`cap-${i}`, capability, "bg-green-50 text-green-700 ring-green-600/20", "Capability")}
          {/each}
        </div>
      {/if}
      <span class="sr-only" role="status">{copied ? "Copied" : ""}</span>
    </div>
    <button
      type="button"
      class="-mt-0.5 -mr-1.5 ml-auto shrink-0 rounded-md p-1 text-gray-400 max-sm:mt-0 max-sm:flex max-sm:p-2.5 hover:bg-gray-100 hover:text-gray-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
      aria-label="Close"
      onclick={onclose}
    >
      <Icon svg={closeIcon} class="size-4" />
    </button>
  </div>

  {#if details.length > 0}
    <!-- One grid, so that labels and values line up in every row: a label and a value per row, or
         two of each in a full-width panel wide enough for them. Each detail is a subgrid of it. -->
    <div class="@container px-2.5 pt-0.5 pb-1.5">
      <dl
        class="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 max-sm:@min-[34rem]:grid-cols-[max-content_auto_max-content_1fr]"
      >
        {#each details as detail (detail.key)}
          <div class={["col-span-2 grid grid-cols-subgrid", !detail.paired && "max-sm:@min-[34rem]:col-span-4"]}>
            <dt class="flex items-center gap-2 text-gray-500">
              <!-- The label right after the icon: a space between them would be in the label's text. -->
              {#if detail.icon}
                <Icon svg={detail.icon} class="size-4 text-gray-400" />
              {:else}
                <span class="size-4 shrink-0"></span>
              {/if}{detail.label}
            </dt>
            <dd class={["break-words text-gray-900", !detail.paired && "max-sm:@min-[34rem]:col-span-3"]}>{detail.value}</dd>
          </div>
        {/each}
      </dl>
    </div>
  {/if}
</section>
