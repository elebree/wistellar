<script lang="ts" module>
  export interface MenuItem {
    id: string;
    label: string;
    /** SVG markup, imported with `?raw` from `src/lib/icons/`. Use `currentColor` so it follows the text. */
    icon?: string;
    action: () => void;
    /** Hides the item when it returns false. Re-evaluated whenever state it reads changes. */
    visible?: () => boolean;
  }
</script>

<script lang="ts">
  import { tick, type Snippet } from "svelte";
  import Icon from "./Icon.svelte";
  import menuIcon from "#lib/icons/menu.svg?raw";

  interface Props {
    items: MenuItem[];
    /** Shown above the items, e.g. who is signed in. It also becomes the menu's accessible name. */
    header?: Snippet;
    label?: string;
  }

  let { items, header, label = "Menu" }: Props = $props();

  const id = $props.id();
  let open = $state(false);
  let root: HTMLElement;
  let button: HTMLButtonElement;
  let menu = $state<HTMLElement>();

  let visibleItems = $derived(items.filter((item) => item.visible?.() ?? true));

  function menuItems() {
    return Array.from(menu?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
  }

  /** Focuses the item at `index`, wrapping around; -1 is the last item. */
  function focusItem(index: number) {
    const elements = menuItems();
    if (elements.length === 0) return;
    elements[(index + elements.length) % elements.length].focus();
  }

  async function show(index = 0) {
    open = true;
    await tick();
    focusItem(index);
  }

  function hide(returnFocus: boolean) {
    open = false;
    if (returnFocus) button.focus();
  }

  function select(item: MenuItem) {
    // Return focus first, so a dialog opened by the action gives it back to the button on close.
    hide(true);
    item.action();
  }

  function onButtonKeydown(e: KeyboardEvent) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      show(e.key === "ArrowUp" ? -1 : 0);
    } else if (e.key === "Escape" && open) {
      hide(true);
    }
  }

  function onMenuKeydown(e: KeyboardEvent) {
    const current = menuItems().indexOf(document.activeElement as HTMLElement);
    const moves: Record<string, number> = {
      ArrowDown: current + 1,
      ArrowUp: current - 1,
      Home: 0,
      End: -1,
    };

    if (e.key in moves) {
      e.preventDefault();
      focusItem(moves[e.key]);
    } else if (e.key === "Escape") {
      e.preventDefault();
      hide(true);
    } else if (e.key === "Tab") {
      hide(false);
    }
  }

  function onWindowPointerDown(e: PointerEvent) {
    if (open && !root.contains(e.target as Node)) hide(false);
  }
</script>

<!-- Capture phase, so the map's own pointer handling can't swallow the event. -->
<svelte:window onpointerdowncapture={onWindowPointerDown} />

<div class="app-menu maplibregl-ctrl" bind:this={root}>
  <button
    bind:this={button}
    id="{id}-button"
    type="button"
    class="menu-button"
    aria-label={label}
    aria-haspopup="menu"
    aria-expanded={open}
    aria-controls={open ? `${id}-menu` : undefined}
    onclick={() => (open ? hide(false) : show())}
    onkeydown={onButtonKeydown}
  >
    <Icon svg={menuIcon} class="size-5" />
  </button>

  {#if open}
    <div
      class="absolute top-full right-0 mt-2 w-56 overflow-hidden rounded-lg bg-white p-1.5 shadow-lg ring-1 ring-black/5"
    >
      {#if header}
        <div id="{id}-header" class="mb-1.5 border-b border-gray-100 px-2.5 pt-1 pb-2.5">
          {@render header()}
        </div>
      {/if}
      <div
        bind:this={menu}
        id="{id}-menu"
        role="menu"
        tabindex="-1"
        aria-labelledby={header ? `${id}-header` : `${id}-button`}
        onkeydown={onMenuKeydown}
      >
        {#each visibleItems as item (item.id)}
          <button
            type="button"
            role="menuitem"
            tabindex="-1"
            class="flex w-full items-center gap-3 rounded-md px-2.5 py-2 text-left text-sm text-gray-700 hover:bg-gray-100 focus:bg-gray-100 focus:outline-none"
            onclick={() => select(item)}
          >
            {#if item.icon}
              <Icon svg={item.icon} class="size-5 text-gray-400" />
            {/if}
            {item.label}
          </button>
        {/each}
      </div>
    </div>
  {/if}
</div>

<style>
  /* Built like MapLibre's control groups: the white and the ring sit on the container and the button
     stays transparent, so MapLibre's own `.maplibregl-ctrl button` hover tint lands on white. */
  .app-menu {
    border-radius: 50%;
    background: #fff;
    box-shadow: 0 0 0 2px rgba(0, 0, 0, 0.1);
  }

  /* 36px matches the filter box's height. */
  .menu-button {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 36px;
    height: 36px;
    border-radius: 50%;
    background-color: transparent;
    color: #333;
  }

  .menu-button:focus-visible {
    outline: none;
    box-shadow: 0 0 2px 2px #0096ff;
  }
</style>
