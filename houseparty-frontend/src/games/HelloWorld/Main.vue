<script setup lang="ts">
defineOptions({ name: 'HelloWorldGame' })
</script>

<template>
  <section class="hello-world">Hello World</section>
</template>

<style scoped>
.hello-world {
  width: 100%;
  height: 100%;
  min-height: 100%;
  display: grid;
  place-items: center;
  border: 1px solid red;
  font-size: 20px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}
</style>
