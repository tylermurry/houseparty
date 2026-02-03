import { computed, onBeforeUnmount, onMounted, ref, watch, type Ref, type ShallowRef } from 'vue'
import type { HubConnection } from '@microsoft/signalr'
import type { PlayerEntry } from '@/services/roomService'

const GRID_SIZE = 2048
const PRESENCE_INTERVAL_MS = 1000 / 30

type MousePresenceUpdate = { playerNumber: number; name: string; x: number; y: number }
type RawMousePresenceUpdate =
  | MousePresenceUpdate
  | { PlayerNumber?: string | number; Name?: string; X?: number; Y?: number }

type UseMousePresenceOptions = {
  roomId: Ref<string>
  playerName: Ref<string>
  playerNumber: Ref<number | null>
  hasJoined: Ref<boolean>
  isConnected: Ref<boolean>
  players: Ref<PlayerEntry[]>
  connection: Ref<HubConnection | null> | ShallowRef<HubConnection | null>
}

export function useMousePresence(options: UseMousePresenceOptions) {
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

  const playerNameMap = computed(
    () => new Map(options.players.value.map((player) => [player.number, player.name])),
  )
  const visiblePresence = computed(() =>
    presenceList.value.filter((presence) => presence.playerNumber !== options.playerNumber.value),
  )

  const apiBaseUrl = (import.meta.env.VITE_BACKEND_API_URL ?? '').replace(/\/$/, '')
  const mousePresenceUrl = computed(() =>
    apiBaseUrl
      ? `${apiBaseUrl}/api/rooms/${options.roomId.value}/mouse`
      : `/api/rooms/${options.roomId.value}/mouse`,
  )

  const handlePointerMove = (event: PointerEvent) => {
    latestPointer.value = { x: event.clientX, y: event.clientY }
  }
  const handlePointerLeave = () => {
    latestPointer.value = null
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

  async function sendMousePresence(quantized: { x: number; y: number }) {
    if (!options.hasJoined.value || options.playerNumber.value === null) {
      return
    }

    const trimmedName = options.playerName.value.trim().slice(0, 10)
    if (!trimmedName) {
      return
    }

    try {
      await fetch(mousePresenceUrl.value, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          playerNumber: options.playerNumber.value,
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
      if (
        lastSentMouse.value &&
        lastSentMouse.value.x === quantized.x &&
        lastSentMouse.value.y === quantized.y
      ) {
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

  function advancePresence() {
    const smoothing = 0.2
    presenceList.value.forEach((presence) => {
      presence.currentX += (presence.targetX - presence.currentX) * smoothing
      presence.currentY += (presence.targetY - presence.currentY) * smoothing
    })
  }

  const handlePresenceUpdate = (update: RawMousePresenceUpdate) => {
    const normalized = normalizeMousePresence(update)
    if (!normalized) return
    upsertPresence(normalized)
  }

  watch(
    options.connection,
    (next, prev) => {
      if (prev) {
        prev.off('mousePresenceUpdated', handlePresenceUpdate)
      }
      if (next) {
        next.on('mousePresenceUpdated', handlePresenceUpdate)
      }
    },
    { immediate: true },
  )

  watch([options.isConnected, options.hasJoined], ([connected, joined]) => {
    if (connected && joined) {
      startPresenceUpdates()
    } else {
      stopPresenceUpdates()
    }
  })

  watch(
    options.players,
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

  onMounted(() => {
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerleave', handlePointerLeave)
  })

  onBeforeUnmount(() => {
    stopPresenceUpdates()
    if (options.connection.value) {
      options.connection.value.off('mousePresenceUpdated', handlePresenceUpdate)
    }
    window.removeEventListener('pointermove', handlePointerMove)
    window.removeEventListener('pointerleave', handlePointerLeave)
  })

  return {
    presenceList,
    visiblePresence,
    advancePresence,
  }
}
