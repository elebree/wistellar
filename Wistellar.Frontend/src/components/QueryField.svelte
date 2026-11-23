<script lang="ts">
  import { onDestroy } from "svelte";
  import Icon from "./Icon.svelte";
  import codeIcon from "#lib/icons/code-bracket.svg?raw";

  interface Props {
    /** The filter query, all of it, as the link has it. */
    filter: string;
  }

  let { filter = $bindable() }: Props = $props();

  // What is typed, until it goes into the filter: on Enter, on leaving the field, or when the pane
  // closes around it, like the pattern fields. Escape drops it.
  let typed: string | undefined;

  function commit() {
    if (typed !== undefined && typed !== filter) filter = typed;
    typed = undefined;
  }

  // A tap on the map closes the pane, and removes the field, before the field sees a blur.
  onDestroy(commit);
</script>

<div>
  <div class="flex items-center gap-2 text-sm">
    <Icon svg={codeIcon} class="size-5 text-gray-500" />
    <label for="filter" class="font-medium text-gray-900">Query</label>
  </div>
  <p id="filter-hint" class="mt-0.5 pl-7 text-xs text-gray-500">The whole filter, as in the link</p>
  <input
    id="filter"
    name="filter"
    type="text"
    class="mt-1.5 block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
    placeholder="Filter query"
    autocomplete="off"
    autocapitalize="off"
    spellcheck={false}
    enterkeyhint="search"
    aria-describedby="filter-hint"
    value={filter}
    oninput={(e) => (typed = e.currentTarget.value)}
    onkeydown={(e) => {
      if (e.key === "Enter") {
        e.preventDefault();
        commit();
        e.currentTarget.blur();
      } else if (e.key === "Escape") {
        // Then the pane closes.
        typed = undefined;
      }
    }}
    onblur={commit}
  />
</div>
