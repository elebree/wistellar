<script lang="ts">
  import Icon from "./Icon.svelte";
  import closeIcon from "#lib/icons/x-mark.svg?raw";

  interface Props {
    properties: Record<string, unknown>;
    onclose: () => void;
  }

  let { properties, onclose }: Props = $props();

  const headerKeys = new Set(["type", "bssid", "ssid", "cap", "lastlat", "lastlon"]);
  const labels: Record<string, string> = {
    lasttime: "Last seen",
    level: "Level",
    loc: "Observations",
    dist: "Range",
    dur: "Dwell",
    vendor: "Vendor",
  };

  const dateLocale = "de-DE";

  const durationUnits = [
    ["d", 86400],
    ["h", 3600],
    ["min", 60],
    ["s", 1],
  ] as const;

  function formatDuration(seconds: number): string {
    let rest = Math.round(seconds);
    const amounts = durationUnits.map(([unit, size]) => {
      const amount = Math.floor(rest / size);
      rest -= amount * size;
      return { amount, unit };
    });
    const first = amounts.findIndex((a) => a.amount > 0);
    if (first < 0) return "0 s";
    return amounts
      .slice(first, first + 2)
      .filter((a) => a.amount > 0)
      .map((a) => `${a.amount} ${a.unit}`)
      .join(" ");
  }

  function formatValue(key: string, value: unknown): string {
    switch (key) {
      case "lasttime":
        return new Date(value as number).toLocaleString(dateLocale);
      case "dur": // dwell: seconds between the first and last observation
        return formatDuration(Number(value));
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

  let details = $derived(
    Object.entries(properties)
      .filter(([key, value]) => !headerKeys.has(key) && present(value))
      .map(([key, value]) => ({
        key,
        label: labels[key] ?? key,
        value: formatValue(key, value),
      })),
  );

  const chip = "inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset break-all";
</script>

<div
  class="w-max max-w-72 rounded-lg bg-white p-1.5 font-sans text-sm text-gray-700 shadow-lg ring-1 ring-black/5"
>
  <div
    class={[
      "flex items-start gap-2 px-2.5 pt-1",
      details.length > 0 ? "mb-1.5 border-b border-gray-100 pb-2.5" : "pb-1",
    ]}
  >
    <div class="flex min-w-0 flex-col gap-1.5">
      <div class="flex flex-wrap gap-1">
        {#if present(properties.type)}
          <span class="{chip} bg-blue-50 text-blue-700 ring-blue-700/10">{properties.type}</span>
        {/if}
        {#if present(properties.bssid)}
          <span class="{chip} bg-gray-50 text-gray-600 ring-gray-500/10">{properties.bssid}</span>
        {/if}
      </div>
      {#if present(properties.ssid)}
        <div class="flex">
          <span class="{chip} bg-yellow-50 text-yellow-800 ring-yellow-600/20">{properties.ssid}</span>
        </div>
      {/if}
      {#if capabilities.length > 0}
        <div class="flex flex-wrap gap-1">
          {#each capabilities as capability, i (i)}
            <span class="{chip} bg-green-50 text-green-700 ring-green-600/20">{capability}</span>
          {/each}
        </div>
      {/if}
    </div>
    <button
      type="button"
      class="-mt-0.5 -mr-1.5 ml-auto shrink-0 rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-600"
      aria-label="Close"
      onclick={onclose}
    >
      <Icon svg={closeIcon} class="size-4" />
    </button>
  </div>

  {#if details.length > 0}
    <dl class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 px-2.5 pt-0.5 pb-1.5">
      {#each details as detail (detail.key)}
        <dt class="text-gray-500">{detail.label}</dt>
        <dd class="text-gray-900">{detail.value}</dd>
      {/each}
    </dl>
  {/if}
</div>
