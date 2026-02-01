<script setup lang="ts">
import { onMounted } from 'vue';

import { initStarfield } from "@/assets/scripts/starfield";
import { initCursor } from "@/assets/scripts/cursor";

onMounted(() => {
  initStarfield('starfield');
  initCursor();
});
</script>

<template>
  <div id="starfield">
    <router-view />
  </div>
</template>

<style>
:root {
  color-scheme: light;
}

body {
  margin: 0;
  font-family: 'Courier New', Courier, monospace;
  background: #f6f6f2;
  color: #1c1c1c;
}

#starfield {
  position: relative;
  min-height: 100vh;
  overflow: hidden;
  background: #05061d;
}
</style>
