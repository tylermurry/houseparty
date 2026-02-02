<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import API from '@/api-client/client'
import PlayerRoster from '@/components/PlayerRoster.vue'
import type { RoomPlayer } from '@/api-client/client/types.gen'

const route = useRoute()
const roomId = computed(() => route.params.roomId?.toString() ?? '')
const isConnected = ref(false)
const isConnecting = ref(true)
const connectionError = ref('')
const playerName = ref('')
type PlayerEntry = { number: number; name: string }
type RawPlayer = RoomPlayer | { Number?: string | number; Name?: string }

const playerNumber = ref<number | null>(null)
const players = ref<PlayerEntry[]>([])
const hasJoined = ref(false)
const isJoining = ref(false)
const joinError = ref('')
const copyStatus = ref('')
const roomLink = computed(() => window.location.href)
const nameInputRef = ref<HTMLInputElement | null>(null)
let copyStatusTimer: number | null = null
let connection: HubConnection | null = null

async function negotiateConnection() {
  const response = await API.postApiSignalrNegotiate()
  if (!response.data?.url || !response.data?.accessToken) {
    throw new Error('Failed to negotiate SignalR connection.')
  }
  return response
}

async function joinRoom(connectionId: string, existingPlayerNumber?: number | null) {
  const trimmedName = playerName.value.trim().slice(0, 10)
  if (!trimmedName) {
    joinError.value = 'Name is required.'
    return
  }

  isJoining.value = true
  joinError.value = ''

  try {
    const payload = await API.postApiRoomsByRoomIdJoin({
      path: { roomId: roomId.value },
      body: {
        connectionId,
        name: trimmedName,
        playerNumber: existingPlayerNumber ?? null,
      },
    })

    const responsePlayer = payload.data?.player
    const responsePlayers = payload.data?.players ?? []
    players.value = normalizePlayers(responsePlayers.length > 0 ? responsePlayers : (responsePlayer ? [responsePlayer] : []))
    playerNumber.value = responsePlayer ? toNumber(responsePlayer.number) : playerNumber.value
    hasJoined.value = true
  } catch (error) {
    joinError.value = error instanceof Error ? error.message : 'Failed to join room.'
  } finally {
    isJoining.value = false
  }
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

  connection.on('playerRosterUpdated', (updatedPlayers: RawPlayer[]) => {
    const normalized = normalizePlayers(updatedPlayers)
    if (normalized.length > 0) {
      players.value = normalized
    }
  })

  connection.onreconnected(async (connectionId) => {
    isConnected.value = true
    if (connectionId && hasJoined.value) {
      await joinRoom(connectionId, playerNumber.value)
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
  } catch (error) {
    isConnected.value = false
    connectionError.value = error instanceof Error ? error.message : 'Failed to connect.'
  } finally {
    isConnecting.value = false
  }
}

async function submitName() {
  if (!connection || !isConnected.value || !connection.connectionId) {
    joinError.value = 'Waiting for connection...'
    return
  }
  await joinRoom(connection.connectionId)
}

async function copyLink() {
  copyStatus.value = ''
  if (copyStatusTimer) {
    window.clearTimeout(copyStatusTimer)
    copyStatusTimer = null
  }
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(roomLink.value)
      copyStatus.value = 'Link copied.'
      copyStatusTimer = window.setTimeout(() => {
        copyStatus.value = ''
        copyStatusTimer = null
      }, 3000)
      return
    }
  } catch {
    // Fall back to manual copy prompt below.
  }

  window.prompt('Copy this room link:', roomLink.value)
}

function toNumber(value: string | number) {
  return typeof value === 'number' ? value : Number.parseInt(value, 10)
}

function normalizePlayers(rawPlayers: RawPlayer[]): PlayerEntry[] {
  return rawPlayers
    .map((player) => ({
      number: toNumber('number' in player ? player.number : player.Number),
      name: 'name' in player ? player.name : player.Name,
    }))
    .filter((player) => Number.isFinite(player.number) && Boolean(player.name))
}

onMounted(() => {
  void startConnection()
})

watch([isConnected, hasJoined], async ([connected, joined]) => {
  if (!connected || joined) return
  await nextTick()
  nameInputRef.value?.focus()
})

onBeforeUnmount(() => {
  if (copyStatusTimer) {
    window.clearTimeout(copyStatusTimer)
  }
  void connection?.stop()
})
</script>

<template>
  <main class="room-shell">
    <aside class="room-sidebar">
      <div class="copy-link">
        <button class="primary" @click="copyLink">Copy Link</button>
        <div v-if="copyStatus" class="copy-status">{{ copyStatus }}</div>
      </div>
      <PlayerRoster :players="players" />
    </aside>

    <section class="room-main">
      <section class="panel">
        <p v-if="isConnecting" class="hint">Connecting to room...</p>
        <p v-else-if="connectionError" class="hint error">{{ connectionError }}</p>
      </section>
    </section>

    <div v-if="isConnected && !hasJoined" class="name-overlay">
      <form class="name-card" autocomplete="off" @submit.prevent="submitName">
        <div class="name-title">Enter your name</div>
        <label class="name-label" for="player-name">Display name</label>
        <input
          id="player-name"
          v-model="playerName"
          class="name-input"
          type="text"
          name="player-name"
          autocomplete="new-password"
          autocorrect="off"
          autocapitalize="off"
          spellcheck="false"
          maxlength="10"
          placeholder="e.g. Alex"
          ref="nameInputRef"
        />
        <button type="submit" class="primary" :disabled="isJoining || !playerName.trim()">Join Room</button>
        <div v-if="joinError" class="hint error">{{ joinError }}</div>
      </form>
    </div>
  </main>
</template>

<style scoped>
.room-shell {
  width: 100%;
  margin: 0;
  padding: 32px 24px 60px;
  display: grid;
  grid-template-columns: minmax(180px, 240px) minmax(0, 1fr);
  gap: 32px;
  align-items: start;
}

.room-sidebar {
  display: grid;
  gap: 24px;
  align-content: start;
}

.copy-link {
  display: grid;
  gap: 8px;
  justify-items: start;
}

.copy-status {
  font-size: 12px;
  color: #6c6c6c;
}

.room-main {
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
  padding: 20px;
  display: grid;
  gap: 16px;
}

.panel-row {
  display: flex;
  justify-content: flex-start;
}

button {
  font-family: inherit;
  padding: 10px 18px;
  border: 1px solid #1c1c1c;
  background: #1c1c1c;
  color: #f6f6f2;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

button.primary {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: #ffffff;
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

.name-overlay {
  position: fixed;
  inset: 0;
  background: rgba(5, 6, 29, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  z-index: 10;
}

.name-card {
  width: min(420px, 90vw);
  background: #f6f6f2;
  border: 1px solid #d0d0c8;
  padding: 24px;
  display: grid;
  gap: 12px;
}

.name-title {
  font-size: 18px;
  font-weight: 700;
}

.name-label {
  font-size: 12px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: #6c6c6c;
}

.name-input {
  padding: 10px 12px;
  border: 1px solid #1c1c1c;
  font-family: inherit;
  font-size: 16px;
  background: #ffffff;
  color: #000000;
}

@media (max-width: 800px) {
  .room-shell {
    grid-template-columns: 1fr;
  }
}
</style>
