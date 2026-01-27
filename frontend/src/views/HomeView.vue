<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import API from '@/api-client/client';

const router = useRouter()
const isBusy = ref(false)
const status = ref('')


async function createRoom() {
  status.value = ''
  try {
    isBusy.value = true
    const room = await API.postApiRooms()
    if (!room.data?.id) {
      status.value = 'Failed to create room.'
      return
    }
    await router.push(`/room/${room.data.id}`)
  } catch (error) {
    status.value = 'Failed to create room.'
    console.error(error)
  } finally {
    isBusy.value = false
  }
}
</script>

<template>
  <main class="shell">
    <header>
      <h1>HouseParty</h1>
      <p class="hint">Create a room and share the link.</p>
    </header>

    <section class="panel">
      <button :disabled="isBusy" @click="createRoom">Create Room</button>
      <p v-if="status" class="status">{{ status }}</p>
    </section>
  </main>
</template>

<style scoped>
.shell {
  max-width: 640px;
  margin: 80px auto;
  padding: 0 20px 60px;
  display: grid;
  gap: 24px;
}

header h1 {
  margin: 0 0 8px;
  font-size: 28px;
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

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.status {
  margin-top: 12px;
  font-size: 14px;
}
</style>
