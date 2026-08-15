<script lang="ts">
  import "../../app.css";
  import Login from "../../components/Login.svelte";
  import { goto } from "$app/navigation";
</script>

<div class="app">
  <Login
    loggedIn={() => {
      goto('/');
    }}
  />
</div>
<style>
  :global(body) {
    margin: 0;
  }
</style>
