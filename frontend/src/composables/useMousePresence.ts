import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import type { NormalizedMouseEvent, PlayerHandle, RoomHandle } from '@houseparty/sdk'
import type { PlayerEntry } from '@/types/player'

type UseMousePresenceOptions = {
  room: Ref<RoomHandle<unknown> | null>
  player: Ref<PlayerHandle | null>
  hasJoined: Ref<boolean>
  players: Ref<PlayerEntry[]>
}

export function useMousePresence(options: UseMousePresenceOptions) {
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
  let mousePresenceDisposer: (() => void) | null = null

  const playerNameMap = computed(
    () => new Map(options.players.value.map((player) => [player.number, player.name])),
  )
  const visiblePresence = computed(() =>
    presenceList.value.filter((presence) => presence.playerNumber !== options.player.value?.number),
  )

  function playerColor(number: number) {
    const normalized = ((number - 1) % 20) + 1
    return `var(--color-player-${normalized})`
  }

  function upsertPresence(update: NormalizedMouseEvent) {
    const targetX = Math.min(1, Math.max(0, update.normalizedX))
    const targetY = Math.min(1, Math.max(0, update.normalizedY))
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

  function disposeMousePresenceListener() {
    if (mousePresenceDisposer) {
      mousePresenceDisposer()
      mousePresenceDisposer = null
    }
  }

  watch([options.hasJoined, options.room, options.player], ([joined, room, player]) => {
    disposeMousePresenceListener()

    if (!joined || !room || !player) {
      return
    }

    mousePresenceDisposer = room.useMousePresence(player, (mouseEvent) => {
      upsertPresence(mouseEvent)
    })
  })

  function advancePresence() {
    const smoothing = 0.2
    presenceList.value.forEach((presence) => {
      presence.currentX += (presence.targetX - presence.currentX) * smoothing
      presence.currentY += (presence.targetY - presence.currentY) * smoothing
    })
  }

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

  onBeforeUnmount(() => {
    disposeMousePresenceListener()
  })

  return {
    presenceList,
    visiblePresence,
    advancePresence,
  }
}
