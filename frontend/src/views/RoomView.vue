<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import API from '@/api-client/client'
import PlayerRoster from '@/components/PlayerRoster.vue'
import type { RoomPlayer } from '@/api-client/client/types.gen'

const GRID_SIZE = 2048
const PRESENCE_INTERVAL_MS = 1000 / 30

const route = useRoute()
const roomId = computed(() => route.params.roomId?.toString() ?? '')
const isConnected = ref(false)
const isConnecting = ref(true)
const connectionError = ref('')
const playerName = ref('')
type PlayerEntry = { number: number; name: string }
type RawPlayer = RoomPlayer | { Number?: string | number; Name?: string }
type MousePresenceUpdate = { playerNumber: number; name: string; x: number; y: number }
type RawMousePresenceUpdate = MousePresenceUpdate | { PlayerNumber?: string | number; Name?: string; X?: number; Y?: number }

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
const lastSentMouse = ref<{ x: number; y: number } | null>(null)
const latestPointer = ref<{ x: number; y: number } | null>(null)
const presenceList = ref<
  Array<{
    playerNumber: number
    name: string
    color: string
    currentX: number
    currentY: number
    targetX: number
    targetY: number
  }>
>([])
let presenceTimer: number | null = null
let presenceAnimationFrame: number | null = null

const playerNameMap = computed(() => new Map(players.value.map((player) => [player.number, player.name])))
const visiblePresence = computed(() =>
  presenceList.value.filter((presence) => presence.playerNumber !== playerNumber.value),
)

const apiBaseUrl = (import.meta.env.VITE_BACKEND_API_URL ?? '').replace(/\/$/, '')
const mousePresenceUrl = computed(() =>
  apiBaseUrl ? `${apiBaseUrl}/api/rooms/${roomId.value}/mouse` : `/api/rooms/${roomId.value}/mouse`,
)
const handlePointerMove = (event: PointerEvent) => {
  latestPointer.value = { x: event.clientX, y: event.clientY }
}
const handlePointerLeave = () => {
  latestPointer.value = null
}

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

  connection.on('mousePresenceUpdated', (update: RawMousePresenceUpdate) => {
    const normalized = normalizeMousePresence(update)
    if (!normalized) return
    upsertPresence(normalized)
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

function toNumber(value: string | number | undefined | null) {
  if (typeof value === 'number') {
    return value
  }
  if (typeof value === 'string') {
    return Number.parseInt(value, 10)
  }
  return Number.NaN
}

function normalizePlayers(rawPlayers: RawPlayer[]): PlayerEntry[] {
  return rawPlayers
    .map((player) => ({
      number: toNumber('number' in player ? player.number : player.Number),
      name: ('name' in player ? player.name : player.Name) ?? '',
    }))
    .filter((player) => Number.isFinite(player.number) && player.name.length > 0)
}

function normalizeMousePresence(update: RawMousePresenceUpdate): MousePresenceUpdate | null {
  const number = toNumber('playerNumber' in update ? update.playerNumber : update.PlayerNumber)
  const name = 'name' in update ? update.name : update.Name
  const rawX = 'x' in update ? update.x : update.X
  const rawY = 'y' in update ? update.y : update.Y
  const xValue = typeof rawX === 'number' ? rawX : Number(rawX)
  const yValue = typeof rawY === 'number' ? rawY : Number(rawY)

  if (!Number.isFinite(number) || !Number.isFinite(xValue) || !Number.isFinite(yValue)) {
    return null
  }

  return {
    playerNumber: number,
    name: name ?? '',
    x: xValue,
    y: yValue,
  }
}

function playerColor(number: number) {
  const normalized = ((number - 1) % 20) + 1
  return `var(--color-player-${normalized})`
}

function quantizePointer(pointer: { x: number; y: number }) {
  const width = window.innerWidth
  const height = window.innerHeight
  if (width <= 0 || height <= 0) {
    return null
  }

  const normalizedX = Math.min(1, Math.max(0, pointer.x / width))
  const normalizedY = Math.min(1, Math.max(0, pointer.y / height))

  const x = Math.min(GRID_SIZE - 1, Math.max(0, Math.floor(normalizedX * GRID_SIZE)))
  const y = Math.min(GRID_SIZE - 1, Math.max(0, Math.floor(normalizedY * GRID_SIZE)))

  return { x, y }
}

function upsertPresence(update: MousePresenceUpdate) {
  const targetX = Math.min(1, Math.max(0, update.x / GRID_SIZE))
  const targetY = Math.min(1, Math.max(0, update.y / GRID_SIZE))
  const resolvedName = playerNameMap.value.get(update.playerNumber) ?? update.name ?? ''
  const color = playerColor(update.playerNumber)
  const existing = presenceList.value.find((presence) => presence.playerNumber === update.playerNumber)

  if (!existing) {
    presenceList.value.push({
      playerNumber: update.playerNumber,
      name: resolvedName,
      color,
      currentX: targetX,
      currentY: targetY,
      targetX,
      targetY,
    })
    return
  }

  existing.name = resolvedName
  existing.color = color
  existing.targetX = targetX
  existing.targetY = targetY
}

function animatePresence() {
  const smoothing = 0.2
  presenceList.value.forEach((presence) => {
    presence.currentX += (presence.targetX - presence.currentX) * smoothing
    presence.currentY += (presence.targetY - presence.currentY) * smoothing
  })
  presenceAnimationFrame = window.requestAnimationFrame(animatePresence)
}

async function sendMousePresence(quantized: { x: number; y: number }) {
  if (!hasJoined.value || playerNumber.value === null) {
    return
  }

  const trimmedName = playerName.value.trim().slice(0, 10)
  if (!trimmedName) {
    return
  }

  try {
    await fetch(mousePresenceUrl.value, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        playerNumber: playerNumber.value,
        name: trimmedName,
        x: quantized.x,
        y: quantized.y,
      }),
    })
  } catch {
    // Ignore transient mouse update errors.
  }
}

function startPresenceUpdates() {
  if (presenceTimer) return
  presenceTimer = window.setInterval(() => {
    if (!latestPointer.value) return
    const quantized = quantizePointer(latestPointer.value)
    if (!quantized) return
    if (lastSentMouse.value && lastSentMouse.value.x === quantized.x && lastSentMouse.value.y === quantized.y) {
      return
    }
    lastSentMouse.value = quantized
    void sendMousePresence(quantized)
  }, PRESENCE_INTERVAL_MS)
}

function stopPresenceUpdates() {
  if (!presenceTimer) return
  window.clearInterval(presenceTimer)
  presenceTimer = null
  lastSentMouse.value = null
}

onMounted(() => {
  void startConnection()
  animatePresence()
  window.addEventListener('pointermove', handlePointerMove)
  window.addEventListener('pointerleave', handlePointerLeave)
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
  stopPresenceUpdates()
  if (presenceAnimationFrame) {
    window.cancelAnimationFrame(presenceAnimationFrame)
    presenceAnimationFrame = null
  }
  window.removeEventListener('pointermove', handlePointerMove)
  window.removeEventListener('pointerleave', handlePointerLeave)
  void connection?.stop()
})

watch([isConnected, hasJoined], ([connected, joined]) => {
  if (connected && joined) {
    startPresenceUpdates()
  } else {
    stopPresenceUpdates()
  }
})

watch(
  players,
  (updatedPlayers) => {
    const updatedMap = new Map(updatedPlayers.map((player) => [player.number, player.name]))
    presenceList.value.forEach((presence) => {
      const updatedName = updatedMap.get(presence.playerNumber)
      if (updatedName) {
        presence.name = updatedName
      }
    })
  },
  { deep: true },
)
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

    <div class="presence-layer">
      <div
        v-for="presence in visiblePresence"
        :key="presence.playerNumber"
        class="presence-cursor"
        :style="{
          left: `${presence.currentX * 100}%`,
          top: `${presence.currentY * 100}%`,
        }"
      >
        <div class="presence-pointer"></div>
        <div class="presence-label" :style="{ color: presence.color }">{{ presence.name }}</div>
      </div>
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

.presence-layer {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 8;
}

.presence-cursor {
  position: absolute;
  transform: translate(-50%, -50%);
  display: grid;
  justify-items: center;
  gap: 6px;
}

.presence-pointer {
  width: 24px;
  height: 32px;
  background-image: url('@/assets/images/hand.png');
  background-size: 100% 100%;
  background-repeat: no-repeat;
}

.presence-label {
  font-size: 10pt;
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
