<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import PlayerRoster from '@/components/PlayerRoster.vue'
import { travelStarfieldDown } from '@/assets/scripts/starfield'
import { useRoomConnection } from '@/composables/useRoomConnection'
import { useRoomJoin } from '@/composables/useRoomJoin'
import { useMousePresence } from '@/composables/useMousePresence'
import { useCopyLink } from '@/composables/useCopyLink'
import { useGameCatalog } from '@/composables/useGameCatalog'
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

const { games, activeGame, selectGame } = useGameCatalog()

const nameInputRef = ref<HTMLInputElement | null>(null)
const gameGridRef = ref<HTMLElement | null>(null)
let namePromptTimer: number | null = null
let presenceAnimationFrame: number | null = null
let gridResizeObserver: ResizeObserver | null = null
let gameTransitionTimer: number | null = null
let gameHoverTimer: number | null = null
const showNamePrompt = ref(false)
const showSidebar = ref(false)
const showGameList = ref(true)
const showGameArea = ref(false)
const pendingGameId = ref<string | null>(null)
const isGameTransitioning = ref(false)
const allowGameHover = ref(false)
const starfieldTravelMs = 1500
const gameTransitionTravelMs = starfieldTravelMs * 2
const animationLeadMs = 200
const animationStartDelayMs = Math.max(starfieldTravelMs - animationLeadMs, 0)
const animationVars = { '--animation-start-delay': `${animationStartDelayMs}ms` }
const gameGridColumns = ref(1)
const visibleGames = computed(() => (showGameList.value ? games : []))
const gameTitleLeaveDelayMs = computed(() => {
  const totalCards = games.length
  const lastDelay = totalCards > 0 ? (totalCards - 1) * 90 : 0
  const leaveDuration = 520
  const earlyOffset = 200
  return Math.max(lastDelay + leaveDuration - earlyOffset, 0)
})

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

function updateGameGridColumns() {
  if (!gameGridRef.value) return
  const columns = window
    .getComputedStyle(gameGridRef.value)
    .gridTemplateColumns.split(' ')
    .filter(Boolean).length
  gameGridColumns.value = Math.max(columns, 1)
}

function getGameDelay(index: number) {
  const columns = gameGridColumns.value || 1
  const row = Math.floor(index / columns)
  const col = index % columns
  return (row * columns + col) * 90
}

function getGameLeaveDelay(index: number) {
  const columns = gameGridColumns.value || 1
  const row = Math.floor(index / columns)
  const col = index % columns
  const order = row * columns + col
  const total = games.length
  const reverseOrder = Math.max(total - 1 - order, 0)
  return reverseOrder * 90
}

function scheduleGameHoverEnable() {
  if (gameHoverTimer) {
    window.clearTimeout(gameHoverTimer)
  }
  const totalCards = games.length
  const lastDelay = totalCards > 0 ? (totalCards - 1) * 90 : 0
  const entranceDuration = 520
  const totalDelay = animationStartDelayMs + lastDelay + entranceDuration
  allowGameHover.value = false
  gameHoverTimer = window.setTimeout(() => {
    allowGameHover.value = true
    gameHoverTimer = null
  }, totalDelay)
}

function handleGameSelect(gameId: string) {
  if (isGameTransitioning.value || activeGame.value) return
  pendingGameId.value = gameId
  isGameTransitioning.value = true
  showGameList.value = false
  travelStarfieldDown(gameTransitionTravelMs)
  if (gameTransitionTimer) {
    window.clearTimeout(gameTransitionTimer)
  }
  gameTransitionTimer = window.setTimeout(() => {
    if (pendingGameId.value) {
      selectGame(pendingGameId.value)
    }
    showGameArea.value = true
    isGameTransitioning.value = false
    gameTransitionTimer = null
  }, gameTransitionTravelMs)
}

onMounted(() => {
  void startConnection()
  animatePresence()
  gridResizeObserver = new ResizeObserver(() => {
    updateGameGridColumns()
  })
  if (gameGridRef.value) {
    gridResizeObserver.observe(gameGridRef.value)
    updateGameGridColumns()
  }
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
  if (joined) {
    travelStarfieldDown(starfieldTravelMs)
  }
  showSidebar.value = joined
  if (joined) {
    void nextTick().then(() => {
      updateGameGridColumns()
    })
  } else {
    showGameList.value = true
    showGameArea.value = false
    pendingGameId.value = null
    isGameTransitioning.value = false
    allowGameHover.value = false
  }
})

watch(
  () => games.length,
  async () => {
    await nextTick()
    updateGameGridColumns()
    if (showGameList.value) {
      scheduleGameHoverEnable()
    }
  },
)

watch(showGameList, (visible) => {
  if (visible) {
    scheduleGameHoverEnable()
  } else {
    allowGameHover.value = false
    if (gameHoverTimer) {
      window.clearTimeout(gameHoverTimer)
      gameHoverTimer = null
    }
  }
})

onBeforeUnmount(() => {
  if (namePromptTimer) {
    window.clearTimeout(namePromptTimer)
  }
  if (presenceAnimationFrame) {
    window.cancelAnimationFrame(presenceAnimationFrame)
    presenceAnimationFrame = null
  }
  if (gameTransitionTimer) {
    window.clearTimeout(gameTransitionTimer)
    gameTransitionTimer = null
  }
  if (gameHoverTimer) {
    window.clearTimeout(gameHoverTimer)
    gameHoverTimer = null
  }
  if (gridResizeObserver) {
    gridResizeObserver.disconnect()
    gridResizeObserver = null
  }
})
</script>

<template>
  <main class="room-shell" :style="animationVars">
    <header class="room-header">
      <section class="room-status">
        <p v-if="isConnecting" class="hint">Connecting to room...</p>
        <p v-else-if="connectionError" class="hint error">{{ connectionError }}</p>
      </section>
      <Transition name="sidebar-fly">
        <div v-if="showSidebar" class="copy-link" :style="animationVars">
          <button class="primary" @click="copyLink">Copy Link</button>
          <div v-if="copyStatus" class="copy-status">{{ copyStatus }}</div>
        </div>
      </Transition>
    </header>

    <aside class="room-sidebar">
      <Transition name="sidebar-fly">
        <PlayerRoster v-if="showSidebar" :players="players" :style="animationVars" />
      </Transition>
    </aside>

    <section class="room-main">
      <section v-if="hasJoined && !activeGame" class="panel game-panel">
        <div class="game-header">
          <div>
            <Transition name="games" appear>
              <div
                v-if="showGameList"
                class="game-title"
                :style="{
                  '--games-delay': `${animationStartDelayMs}ms`,
                  '--games-title-leave-delay': `${gameTitleLeaveDelayMs}ms`,
                }"
              >
                Choose a game
              </div>
            </Transition>
          </div>
        </div>

        <TransitionGroup
          name="games"
          tag="div"
          class="game-grid"
          appear
          :style="animationVars"
          ref="gameGridRef"
        >
          <article
            v-for="(game, index) in visibleGames"
            :key="game.id"
            class="game-card"
            :class="{ 'hover-ready': allowGameHover }"
            role="button"
            tabindex="0"
            :style="{
              '--games-delay': `${animationStartDelayMs + getGameDelay(index)}ms`,
              '--games-leave-delay': `${getGameLeaveDelay(index)}ms`,
            }"
            @click="handleGameSelect(game.id)"
            @keydown.enter="handleGameSelect(game.id)"
            @keydown.space.prevent="handleGameSelect(game.id)"
          >
            <div class="game-card-body">
              <div class="game-card-title">{{ game.title }}</div>
            </div>
            <div class="game-thumb">
              <img :src="game.thumbnailUrl" :alt="`${game.title} preview`" />
            </div>
            <div class="game-card-body">
              <div class="game-card-description">{{ game.description }}</div>
            </div>
          </article>
        </TransitionGroup>
      </section>

      <Transition name="game-area-fade">
        <section v-if="hasJoined && activeGame && showGameArea" class="game-area">
          <component :is="activeGame.component" />
        </section>
      </Transition>
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
  padding: 1rem;
  box-sizing: border-box;
  display: grid;
  grid-template-columns: minmax(180px, 240px) minmax(0, 1fr);
  grid-template-rows: auto 1fr;
  row-gap: 1rem;
  align-items: stretch;
  height: 100vh;
}

.room-header {
  grid-column: 1 / -1;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-start;
  min-height: 53px;
}

.room-status {
  display: grid;
  gap: 4px;
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
  color: var(--text-light-constrast);
}

.sidebar-fly-enter-active {
  transition: opacity 720ms ease, transform 720ms ease;
  transition-delay: var(--animation-start-delay, 0ms);
}

.sidebar-fly-enter-from {
  opacity: 0;
  transform: translateX(-40px);
}

.sidebar-fly-enter-to {
  opacity: 1;
  transform: translateX(0);
}

.games-enter-active,
.games-appear-active,
.games-leave-active {
  transition: opacity 520ms ease, transform 520ms ease;
}

.games-enter-from,
.games-appear-from {
  opacity: 0;
  transform: translateY(18px);
}

.games-enter-to,
.games-appear-to {
  opacity: 1;
  transform: translateY(0);
}

.games-leave-from {
  opacity: 1;
  transform: translateY(0);
}

.games-leave-to {
  opacity: 0;
  transform: translateY(100px);
}

.game-title.games-enter-active,
.game-title.games-appear-active {
  transition-delay: var(--games-delay, 0ms);
}

.game-title.games-leave-active {
  transition-delay: var(--games-title-leave-delay, 0ms);
}

.room-main {
  display: flex;
  flex-direction: column;
  gap: 24px;
  min-height: 0;
  align-content: start;
  overflow: hidden;
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
  color: var(--text-light-constrast);
  font-size: 14px;
}

.panel {
  display: grid;
  gap: 16px;
}

.game-panel {
  gap: 24px;
}

.game-header {
  display: grid;
  gap: 6px;
  min-height: 28px;
}

.game-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--text-dark-constrast);
}

.game-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(4, minmax(0, 400px));
  justify-content: start;
}

.game-card {
  background: white;
  display: grid;
  gap: 12px;
  padding: 14px;
  text-align: left;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-dark-constrast);
  transition: transform 160ms ease, box-shadow 160ms ease;
}

.game-card.hover-ready:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.08);
}

.game-card.games-enter-active,
.game-card.games-appear-active {
  transition: opacity 520ms ease, transform 520ms ease;
  transition-delay: var(--games-delay, 0ms);
}

.game-card.games-leave-active {
  transition: opacity 520ms ease, transform 520ms ease;
  transition-delay: var(--games-leave-delay, 0ms);
}

.game-area-fade-enter-active {
  transition: opacity 420ms ease;
}

.game-area-fade-enter-from {
  opacity: 0;
}

.game-area-fade-enter-to {
  opacity: 1;
}

.game-thumb {
  width: 100%;
  aspect-ratio: 16 / 9;
  overflow: hidden;
  background: #f6f6f2;
}

.game-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.game-card-body {
  display: grid;
  gap: 6px;
}

.game-card-title {
  font-size: 16px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-light-constrast);
}

.game-card-description {
  font-size: 13px;
  color: var(--text-light-constrast);
  line-height: 1.6;
}


.game-area {
  padding: 1rem;
  min-height: 0;
  flex: 1;
  display: flex;
  align-items: stretch;
}

.game-area > * {
  flex: 1;
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
  color: var(--text-light-constrast);
  margin-bottom: 20px;
}

.name-label {
  font-size: 12px;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--text-light-constrast);
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

@media (max-width: 1200px) {
  .game-grid {
    grid-template-columns: repeat(3, minmax(0, 400px));
  }
}

@media (max-width: 900px) {
  .game-grid {
    grid-template-columns: repeat(2, minmax(0, 400px));
  }
}

@media (max-width: 600px) {
  .game-grid {
    grid-template-columns: repeat(1, minmax(0, 400px));
  }
}
</style>
