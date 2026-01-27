<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()
const roomId = computed(() => route.params.roomId?.toString() ?? '')

function leaveRoom() {
  router.push('/')
}
</script>

<template>
  <main class="shell">
    <header>
      <h1>Room {{ roomId }}</h1>
      <p class="hint">Share this link with others.</p>
    </header>

    <section class="panel">
      <div class="room-id">Room ID: {{ roomId }}</div>
    </section>

    <section class="panel panel-row">
      <button class="ghost" @click="leaveRoom">Leave room</button>
    </section>
  </main>
</template>

<style scoped>
.shell {
  max-width: 720px;
  margin: 60px auto;
  padding: 0 20px 60px;
  display: grid;
  gap: 24px;
}

header h1 {
  margin: 0 0 8px;
  font-size: 24px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.hint {
  margin: 0;
  color: #6c6c6c;
  font-size: 14px;
}

.panel {
  border: 1px solid #d0d0c8;
  padding: 20px;
  background: #ffffff;
  display: grid;
  gap: 16px;
}

.panel-row {
  display: flex;
  justify-content: flex-start;
}

.room-id {
  font-size: 24px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

button {
  font-family: inherit;
  padding: 10px 18px;
  border: 1px solid #1c1c1c;
  background: #1c1c1c;
  color: #f6f6f2;
  cursor: pointer;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

button.ghost {
  background: transparent;
  color: #1c1c1c;
}
</style>
