<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import API from '@/api-client/client';

const route = useRoute()
const router = useRouter()
const roomId = computed(() => route.params.roomId?.toString() ?? '')
const count = ref(0)
const isConnected = ref(false)
const isConnecting = ref(true)
const connectionError = ref('')
let connection: HubConnection | null = null

function leaveRoom() {
  router.push('/')
}

async function negotiateConnection() {
  const response = await API.postApiSignalrNegotiate()
  if (!response.data?.url || !response.data?.accessToken) {
    throw new Error('Failed to negotiate SignalR connection.')
  }
  return response
}

async function joinRoom(connectionId: string) {
  const payload = await API.postApiRoomsByRoomIdJoin({ path: { roomId: roomId.value }, body: { connectionId } });
  count.value = payload.data?.counter as number
}

async function startConnection() {
  isConnecting.value = true
  connectionError.value = ''

  const negotiation = await negotiateConnection()

  connection = new HubConnectionBuilder()
    .withUrl(negotiation.data.url, {
      accessTokenFactory: () => negotiation.data.accessToken,
    })
    .withAutomaticReconnect()
    .build()

  connection.on('counterUpdated', (value: number) => {
    count.value = value
  })

  connection.onreconnected(async (connectionId) => {
    isConnected.value = true
    if (connectionId) {
      await joinRoom(connectionId)
    }
  })

  connection.onclose(() => {
    isConnected.value = false
  })

  try {
    await connection.start()
    isConnected.value = true
    if (!connection.connectionId) {
      throw new Error('SignalR connection id missing.')
    }
    await joinRoom(connection.connectionId)
  } catch (error) {
    isConnected.value = false
    connectionError.value = error instanceof Error ? error.message : 'Failed to connect.'
  } finally {
    isConnecting.value = false
  }
}

async function incrementCounter() {
  if (!connection || !isConnected.value) return
  try {
    const payload = await API.postApiRoomsByRoomIdIncrement({ path: { roomId: roomId.value }})
    count.value = payload.data?.counter as number
  } catch (error) {
    connectionError.value = error instanceof Error ? error.message : 'Failed to increment.'
  }
}

onMounted(() => {
  void startConnection()
})

onBeforeUnmount(() => {
  void connection?.stop()
})
</script>

<template>
  <main class="shell">
    <header>
      <h1>Room {{ roomId }}</h1>
      <p class="hint">Share this link with others.</p>
    </header>

    <section class="panel">
      <div class="room-id">Room ID: {{ roomId }}</div>
      <div class="counter">
        <span class="counter-label">Counter</span>
        <span class="counter-value">{{ count }}</span>
      </div>
      <button :disabled="!isConnected" @click="incrementCounter">Increase Count</button>
      <p v-if="isConnecting" class="hint">Connecting to room...</p>
      <p v-else-if="connectionError" class="hint error">{{ connectionError }}</p>
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

.counter {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  padding: 12px 0;
  border-top: 1px solid #e3e3da;
  border-bottom: 1px solid #e3e3da;
}

.counter-label {
  font-size: 12px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #6c6c6c;
}

.counter-value {
  font-size: 32px;
  letter-spacing: 0.06em;
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

button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.error {
  color: #b00020;
}
</style>
