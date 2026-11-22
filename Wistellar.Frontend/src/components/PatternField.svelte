<script lang="ts">
  import { onDestroy } from "svelte";
  import Icon from "./Icon.svelte";
  import { filterWithPattern, patternOf, type PatternFilter } from "#lib/pattern-filters.ts";

  interface Props {
    /** The filter query; the field reads and writes the pattern's parameter. */
    filter: string;
    pattern: PatternFilter;
    /** The element that explains the pattern syntax, shared by the fields. */
    syntaxId: string;
  }

  let { filter = $bindable(), pattern, syntaxId }: Props = $props();

  const id = $props.id();

  // What is typed, until it goes into the filter: on Enter, on leaving the field, or when the pane
  // closes around it, like the filter box. Escape drops it. A plain variable, as `onDestroy` reads
  // it: a writable `$derived` read there gives back an earlier value.
  let typed: string | undefined;

  function commit() {
    if (typed !== undefined && typed.trim() !== patternOf(filter, pattern)) {
      filter = filterWithPattern(filter, pattern, typed);
    }
    typed = undefined;
  }

  // A tap on the map closes the pane, and removes the field, before the field sees a blur.
  onDestroy(commit);
</script>

<div data-pattern={pattern.name}>
  <div class="flex items-center gap-2 text-sm">
    <Icon svg={pattern.icon} class="size-5 text-gray-500" />
    <label for="{id}-input" class="font-medium text-gray-900">{pattern.label}</label>
  </div>
  <p id="{id}-hint" class="mt-0.5 pl-7 text-xs text-gray-500">{pattern.hint}</p>
  <input
    id="{id}-input"
    type="text"
    class="mt-1.5 block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
    placeholder="Any"
    autocomplete="off"
    autocapitalize="off"
    spellcheck={false}
    enterkeyhint="search"
    aria-describedby="{id}-hint {syntaxId}"
    value={patternOf(filter, pattern)}
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
