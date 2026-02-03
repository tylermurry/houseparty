<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import PlayerRoster from '@/components/PlayerRoster.vue'
import { travelStarfieldDown } from '@/assets/scripts/starfield'
import { useRoomConnection } from '@/composables/useRoomConnection'
import { useRoomJoin } from '@/composables/useRoomJoin'
import { useMousePresence } from '@/composables/useMousePresence'
import { useCopyLink } from '@/composables/useCopyLink'
import type { PlayerEntry } from '@/services/roomService'

const route = useRoute()
const roomId = computed(() => route.params.roomId?.toString() ?? '')
const cameFromCreate = computed(() => window.history.state?.fromCreate === true)

const playerName = ref('')
const playerNumber = ref<number | null>(null)
const hasJoined = ref(false)
const players = ref<PlayerEntry[]>([])

const { isJoining, joinError, joinRoom } = useRoomJoin({
  roomId,
  playerName,
  players,
  playerNumber,
  hasJoined,
})

const { connection, isConnected, isConnecting, connectionError, startConnection } = useRoomConnection({
  players,
  onReconnected: async (connectionId) => {
    if (hasJoined.value) {
      await joinRoom(connectionId, playerNumber.value)
    }
  },
})

const { visiblePresence, advancePresence } = useMousePresence({
  roomId,
  playerName,
  playerNumber,
  hasJoined,
  isConnected,
  players,
  connection,
})

const roomLink = computed(() => window.location.href)
const { copyStatus, copyLink } = useCopyLink(roomLink)

const nameInputRef = ref<HTMLInputElement | null>(null)
let namePromptTimer: number | null = null
let sidebarTimer: number | null = null
let presenceAnimationFrame: number | null = null
const showNamePrompt = ref(false)
const showSidebar = ref(false)

async function submitName() {
  if (!connection.value || !isConnected.value || !connection.value.connectionId) {
    joinError.value = 'Waiting for connection...'
    return
  }
  await joinRoom(connection.value.connectionId)
}

function animatePresence() {
  advancePresence()
  presenceAnimationFrame = window.requestAnimationFrame(animatePresence)
}

onMounted(() => {
  void startConnection()
  animatePresence()
})

watch([isConnected, hasJoined, showNamePrompt], async ([connected, joined, promptVisible]) => {
  if (!connected || joined || !promptVisible) return
  await nextTick()
  nameInputRef.value?.focus()
})

watch([isConnected, hasJoined, cameFromCreate], ([connected, joined, fromCreate]) => {
  if (namePromptTimer) {
    window.clearTimeout(namePromptTimer)
    namePromptTimer = null
  }
  if (connected && !joined) {
    showNamePrompt.value = false
    const delayMs = fromCreate ? 0 : 250
    namePromptTimer = window.setTimeout(() => {
      showNamePrompt.value = true
      namePromptTimer = null
    }, delayMs)
  } else {
    showNamePrompt.value = false
  }
})

watch(hasJoined, (joined) => {
  if (sidebarTimer) {
    window.clearTimeout(sidebarTimer)
    sidebarTimer = null
  }
  if (joined) {
    showSidebar.value = false
    travelStarfieldDown(900)
    sidebarTimer = window.setTimeout(() => {
      showSidebar.value = true
      sidebarTimer = null
    }, 1100)
  } else {
    showSidebar.value = false
  }
})

onBeforeUnmount(() => {
  if (namePromptTimer) {
    window.clearTimeout(namePromptTimer)
  }
  if (sidebarTimer) {
    window.clearTimeout(sidebarTimer)
  }
  if (presenceAnimationFrame) {
    window.cancelAnimationFrame(presenceAnimationFrame)
    presenceAnimationFrame = null
  }
})
</script>

<template>
  <main class="room-shell">
    <aside class="room-sidebar">
      <Transition name="sidebar-fly">
        <div v-if="showSidebar" class="copy-link">
          <button class="primary" @click="copyLink">Copy Link</button>
          <div v-if="copyStatus" class="copy-status">{{ copyStatus }}</div>
        </div>
      </Transition>
      <Transition name="sidebar-fly">
        <PlayerRoster v-if="showSidebar" :players="players" />
      </Transition>
    </aside>

    <section class="room-main">
      <section class="panel">
        <p v-if="isConnecting" class="hint">Connecting to room...</p>
        <p v-else-if="connectionError" class="hint error">{{ connectionError }}</p>
      </section>
    </section>

    <Transition name="name-dialog">
      <div v-if="isConnected && !hasJoined && showNamePrompt" class="name-overlay">
        <form class="name-card" autocomplete="off" @submit.prevent="submitName">
          <div class="name-title">Enter your name</div>
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
    </Transition>

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

.sidebar-fly-enter-active {
  transition: opacity 720ms ease, transform 720ms ease;
}

.sidebar-fly-enter-from {
  opacity: 0;
  transform: translateX(-40px);
}

.sidebar-fly-enter-to {
  opacity: 1;
  transform: translateX(0);
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

.name-dialog-enter-active {
  transition: opacity 160ms ease-out;
}

.name-dialog-enter-from,
.name-dialog-leave-to {
  opacity: 0;
}

.name-dialog-enter-active .name-card {
  animation: name-card-pop 220ms ease-out;
}

.name-dialog-leave-active {
  transition: opacity 120ms ease-in;
}

.name-dialog-leave-active .name-card {
  transition: transform 120ms ease-in, opacity 120ms ease-in;
}

.name-dialog-leave-to .name-card {
  opacity: 0;
  transform: scale(0.98);
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
  color: #6c6c6c;
  margin-bottom: 20px;
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

@keyframes name-card-pop {
  0% {
    transform: scale(0.96);
  }
  60% {
    transform: scale(1.03);
  }
  100% {
    transform: scale(1);
  }
}

@media (max-width: 800px) {
  .room-shell {
    grid-template-columns: 1fr;
  }
}
</style>
