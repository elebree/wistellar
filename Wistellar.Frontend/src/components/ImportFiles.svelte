<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import {
    acceptedFileTypes,
    isSupported,
    uploadFile,
    updateNetworkStatistics,
    uploadErrorMessage,
  } from "#lib/upload.ts";

  interface Props {
    onclose: () => void;
    onimport: () => void;
  }

  interface Entry {
    file: File;
    status: "queued" | "uploading" | "imported" | "failed" | "invalid";
    progress: number;
    message: string;
  }

  let { onclose, onimport }: Props = $props();
  const id = $props.id();
  const controller = new AbortController();
  let dialog: HTMLDialogElement;
  let entries = $state<Entry[]>([]);
  let busy = $state(false);
  let finished = $state(false);
  let updating = $state(false);
  let notice = $state("");
  let pressedOnBackdrop = false;

  let pending = $derived(entries.filter((entry) => entry.status === "queued" || entry.status === "failed"));
  let imported = $derived(entries.filter((entry) => entry.status === "imported").length);
  let hasFailures = $derived(entries.some((entry) => entry.status === "failed"));

  onMount(() => dialog.showModal());
  onDestroy(() => {
    controller.abort();
    // Close before removal so the browser restores focus to the menu button.
    dialog?.close();
  });

  function selectFiles(event: Event & { currentTarget: HTMLInputElement }) {
    entries = Array.from(event.currentTarget.files ?? [], (file): Entry => {
      const message = !isSupported(file)
        ? "Unsupported file type. Choose CSV, KML, KMZ, ZIP or SQLite files, optionally gzipped."
        : file.size === 0 ? "This file is empty." : "";
      return { file, status: message ? "invalid" : "queued", progress: 0, message };
    });
    finished = false;
    notice = "";
  }

  async function startImport(event: SubmitEvent) {
    event.preventDefault();
    if (busy || pending.length === 0) return;

    busy = true;
    finished = false;
    notice = "";
    let changed = false;
    try {
      // The API accepts only one upload at a time, including uploads from survey apps.
      for (const entry of pending) {
        entry.status = "uploading";
        entry.progress = 0;
        entry.message = "";
        try {
          entry.message = await uploadFile(entry.file, {
            signal: controller.signal,
            onProgress: (fraction) => (entry.progress = fraction),
          });
          entry.status = "imported";
          changed = true;
        } catch (error) {
          if (controller.signal.aborted) return;
          entry.status = "failed";
          entry.message = uploadErrorMessage(error);
        }
        if (controller.signal.aborted) return;
      }

      if (changed) {
        updating = true;
        try {
          await updateNetworkStatistics(controller.signal);
        } catch (error) {
          if (controller.signal.aborted) return;
          notice = `Files were imported, but network statistics could not be updated. ${uploadErrorMessage(error)}`;
        }
        if (controller.signal.aborted) return;
        onimport();
      }
      finished = true;
    } finally {
      busy = false;
      updating = false;
    }
  }
</script>

<dialog
  bind:this={dialog}
  class="m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-lg overflow-y-auto rounded-lg bg-white p-0 shadow-xl backdrop:bg-gray-500/75"
  aria-labelledby="{id}-title"
  aria-describedby="{id}-description"
  {onclose}
  oncancel={(event) => {
    if (busy) event.preventDefault();
  }}
  onpointerdown={(event) => (pressedOnBackdrop = event.target === dialog)}
  onclick={(event) => {
    if (!busy && pressedOnBackdrop && event.target === dialog) dialog.close();
  }}
>
  <form class="space-y-5 p-6 sm:p-8" onsubmit={startImport}>
    <div>
      <h2 id="{id}-title" class="text-2xl/9 font-bold tracking-tight text-gray-900">Import files</h2>
      <p id="{id}-description" class="mt-2 text-sm text-gray-600">
        Upload WiGLE CSV logs or SQLite backups, OpenCellID or Mylnikov CSV datasets, or wifidb
        KML/KMZ exports. GZip and ZIP archives are supported.
      </p>
    </div>

    <div>
      <label for="{id}-files" class="block text-sm/6 font-medium text-gray-900">Choose files</label>
      <input
        id="{id}-files"
        type="file"
        multiple
        accept={acceptedFileTypes}
        disabled={busy}
        onchange={selectFiles}
        class="mt-2 block w-full rounded-md text-sm text-gray-700 file:mr-3 file:rounded-md file:border-0 file:bg-indigo-50 file:px-3 file:py-2 file:font-semibold file:text-indigo-600 hover:file:bg-indigo-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:opacity-60"
      />
    </div>

    {#if entries.length}
      <ul class="max-h-64 space-y-3 overflow-y-auto rounded-lg border border-gray-200 p-3" aria-label="Selected files">
        {#each entries as entry}
          <li class="space-y-1 text-sm">
            <p class="break-all font-medium text-gray-900">{entry.file.name}</p>
            {#if entry.status === "uploading"}
              <p class="text-gray-600" role="status">
                {entry.progress >= 1 ? "Importing…" : `Uploading… ${Math.round(entry.progress * 100)}%`}
              </p>
              <progress class="block h-2 w-full accent-indigo-600" max="1" value={entry.progress} aria-label="Upload progress for {entry.file.name}"></progress>
            {:else if entry.status === "imported"}
              <p class="text-green-700">Imported{entry.message ? " with warnings" : ""}</p>
            {:else if entry.status === "queued"}
              <p class="text-gray-500">Ready to import</p>
            {/if}
            {#if entry.message}
              <p
                class="whitespace-pre-line break-words {entry.status === 'imported' ? 'text-amber-800' : 'text-red-700'}"
                role={entry.status === "imported" ? "status" : "alert"}
              >{entry.message}</p>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}

    {#if updating}
      <p class="text-sm text-gray-600" role="status">Updating network statistics…</p>
    {:else if finished}
      <p class="text-sm text-gray-700" role="status">
        {imported} of {entries.length} {entries.length === 1 ? "file" : "files"} imported.
      </p>
    {/if}
    {#if notice}
      <p class="rounded-lg bg-amber-50 p-3 text-sm text-amber-800" role="alert">{notice}</p>
    {/if}

    <div class="flex gap-3">
      <button
        type="button"
        disabled={busy}
        class="flex w-full justify-center rounded-md bg-white px-3 py-1.5 text-sm/6 font-semibold text-gray-900 shadow-xs ring-1 ring-gray-300 ring-inset hover:bg-gray-50 disabled:opacity-60"
        onclick={() => dialog.close()}
      >{finished ? "Close" : "Cancel"}</button>
      <button
        type="submit"
        disabled={busy || pending.length === 0}
        class="flex w-full justify-center rounded-md bg-indigo-600 px-3 py-1.5 text-sm/6 font-semibold text-white shadow-xs hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:opacity-60"
      >{busy ? "Importing…" : hasFailures ? "Retry failed files" : "Import files"}</button>
    </div>
  </form>
</dialog>
