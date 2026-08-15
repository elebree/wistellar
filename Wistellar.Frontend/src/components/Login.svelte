<script lang="ts">
  import authService from "$lib/authService";

  let password: string;
  let username: string;
  let message: string = "";
  export let loggedIn: () => void;

  const signIn = async () => {
    try {
      await authService.login(username, password);
      loggedIn();
    } catch (e) {
      message = e?.toString() ?? "";
    }
  };
</script>

<div
  class="relative z-10"
  aria-labelledby="modal-title"
  role="dialog"
  aria-modal="true"
>
  <!--
    Background backdrop, show/hide based on modal state.

    Entering: "ease-out duration-300"
      From: "opacity-0"
      To: "opacity-100"
    Leaving: "ease-in duration-200"
      From: "opacity-100"
      To: "opacity-0"
  -->
  <div
    class="fixed inset-0 bg-gray-500/75 transition-opacity"
    aria-hidden="true"
  ></div>

  <div class="fixed inset-0 z-10 w-screen overflow-y-auto">
    <div
      class="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0"
    >
      <!--
        Modal panel, show/hide based on modal state.

        Entering: "ease-out duration-300"
          From: "opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
          To: "opacity-100 translate-y-0 sm:scale-100"
        Leaving: "ease-in duration-200"
          From: "opacity-100 translate-y-0 sm:scale-100"
          To: "opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
      -->
      <div
        class="relative transform overflow-hidden rounded-lg bg-white text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg"
      >
        <div class="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
          <div
            class="flex min-h-full flex-col justify-center px-6 py-12 lg:px-8"
          >
            <div class="sm:mx-auto sm:w-full sm:max-w-sm">
              <h2
                class="mt-1 text-center text-2xl/9 font-bold tracking-tight text-gray-900"
              >
                Sign in to your account
              </h2>
            </div>

            <div class="mt-1 sm:mx-auto sm:w-full sm:max-w-sm">
              <form class="space-y-6" action="#" method="dialog">
                <div>
                  <label
                    for="username"
                    class="block text-sm/6 font-medium text-gray-900"
                    >Login</label
                  >
                  <div class="mt-2">
                    <input
                      bind:value={username}
                      type="text"
                      name="username"
                      id="username"
                      autocomplete="username"
                      required
                      class="block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
                    />
                  </div>
                </div>

                <div>
                  <div class="flex items-center justify-between">
                    <label
                      for="password"
                      class="block text-sm/6 font-medium text-gray-900"
                      >Password</label
                    >
                  </div>
                  <div class="mt-2">
                    <input
                      bind:value={password}
                      type="password"
                      name="password"
                      id="password"
                      autocomplete="current-password"
                      required
                      class="block w-full rounded-md bg-white px-3 py-1.5 text-base text-gray-900 outline-1 -outline-offset-1 outline-gray-300 placeholder:text-gray-400 focus:outline-2 focus:-outline-offset-2 focus:outline-indigo-600 sm:text-sm/6"
                    />
                  </div>
                </div>
                {#if message}
                  <div
                    class="p-4 mb-4 text-sm text-red-800 rounded-lg bg-red-50 dark:bg-gray-800 dark:text-red-400"
                    role="alert"
                  >
                    {message}
                  </div>
                {/if}
                <div>
                  <button
                    on:click={signIn}
                    type="submit"
                    class="flex w-full justify-center rounded-md bg-indigo-600 px-3 py-1.5 text-sm/6 font-semibold text-white shadow-xs hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
                    >Sign in</button
                  >
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</div>
