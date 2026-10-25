<script lang="ts">
  import axios from "axios";
  import { untrack } from "svelte";
  import { auth } from "#lib/auth.svelte.ts";

  let dialog: HTMLDialogElement;
  let passwordInput = $state<HTMLInputElement>();
  let username = $state("");
  let password = $state("");
  let message = $state("");
  let submitting = $state(false);
  let pressedOnBackdrop = false;

  $effect(() => {
    if (auth.dialog.open && !dialog.open) {
      message = "";
      dialog.showModal();
      // After an expired session the user name is still filled in.
      untrack(() => {
        if (username) passwordInput?.focus();
      });
    } else if (!auth.dialog.open && dialog.open) {
      dialog.close();
    }
  });

  async function signIn(e: SubmitEvent & { currentTarget: HTMLFormElement }) {
    e.preventDefault();
    if (submitting) return;

    const data = new FormData(e.currentTarget);
    username = String(data.get("username") ?? "");
    password = String(data.get("password") ?? "");

    submitting = true;
    message = "";
    try {
      await auth.signIn(username, password);
      auth.closeSignIn();
    } catch (error) {
      message = errorMessage(error);
    } finally {
      submitting = false;
    }
  }

  function errorMessage(error: unknown): string {
    if (axios.isAxiosError(error)) {
      if (!error.response) return "Server not reachable. Check your connection and try again.";
      switch (error.response.status) {
        case 401:
          return "Invalid username or password.";
        case 429:
        case 503:
          // Sign-in allows one request at a time, so this also happens while an upload runs.
          return "The server is busy. Please try again in a moment.";
      }
    }
    console.error("Sign-in failed", error);
    return "Sign-in failed. Please try again.";
  }

  function onclose() {
    password = "";
    auth.closeSignIn();
  }
</script>

<dialog
  bind:this={dialog}
  class="m-auto w-[calc(100%-2rem)] max-w-sm rounded-lg bg-white p-0 shadow-xl backdrop:bg-gray-500/75"
  aria-labelledby="login-title"
  {onclose}
  onpointerdown={(e) => (pressedOnBackdrop = e.target === dialog)}
  onclick={(e) => {
    // The form fills the dialog, so only the backdrop has the dialog itself as target.
    if (pressedOnBackdrop && e.target === dialog) dialog.close();
  }}
>
  <!-- Only in the DOM while open: browsers fill saved passwords into forms that are added, not into
       hidden ones that become visible. Removing it after sign-in also tells them to offer saving. -->
  {#if auth.dialog.open}
    <form class="space-y-6 p-6 sm:p-8" onsubmit={signIn}>
      <h2 id="login-title" class="text-center text-2xl/9 font-bold tracking-tight text-gray-900">
        Sign in
      </h2>

      {#if auth.dialog.notice && !message}
        <p class="rounded-lg bg-amber-50 p-4 text-sm text-amber-800" role="status">
          {auth.dialog.notice}
        </p>
      {/if}

      <div>
        <label for="username" class="block text-sm/6 font-medium text-gray-900">Login</label>
        <input
          bind:value={username}
          type="text"
          name="username"
          id="username"
          autocomplete="username"
          required
          class="mt-2 block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
        />
      </div>

      <div>
        <label for="password" class="block text-sm/6 font-medium text-gray-900">Password</label>
        <input
          bind:this={passwordInput}
          bind:value={password}
          type="password"
          name="password"
          id="password"
          autocomplete="current-password"
          required
          class="mt-2 block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
        />
      </div>

      {#if message}
        <div class="rounded-lg bg-red-50 p-4 text-sm text-red-800" role="alert">
          {message}
        </div>
      {/if}

      <div class="flex gap-3">
        <button
          type="button"
          class="flex w-full justify-center rounded-md bg-white px-3 py-1.5 text-sm/6 font-semibold text-gray-900 shadow-xs ring-1 ring-gray-300 ring-inset hover:bg-gray-50"
          onclick={() => dialog.close()}>Cancel</button
        >
        <button
          type="submit"
          disabled={submitting}
          class="flex w-full justify-center rounded-md bg-indigo-600 px-3 py-1.5 text-sm/6 font-semibold text-white shadow-xs hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-wait disabled:opacity-60"
          >{submitting ? "Signing in…" : "Sign in"}</button
        >
      </div>
    </form>
  {/if}
</dialog>
