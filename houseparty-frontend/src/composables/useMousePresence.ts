import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import type { MousePosition, MousePresenceHandle, RoomHandle } from '@houseparty/client'
import type { PlayerEntry } from '@/types/player'

type UseMousePresenceOptions = {
  room: Ref<RoomHandle<unknown> | null>
  hasJoined: Ref<boolean>
  players: Ref<PlayerEntry[]>
}

export function useMousePresence(options: UseMousePresenceOptions) {
  const presenceList = ref<
    Array<{
      playerNumber: number
      name: string
      color: string
      x: number
      y: number
    }>
  >([])
  const playerNameMap = computed(
    () => new Map(options.players.value.map((player) => [player.number, player.name])),
  )
  const visiblePresence = computed(() => presenceList.value)
  let mousePresence: MousePresenceHandle | null = null
  let mousePresenceDisposer: (() => void) | null = null

  function playerColor(number: number) {
    const normalized = ((number - 1) % 20) + 1
    return `var(--color-player-${normalized})`
  }

  function syncMousePositions(mousePositions: readonly MousePosition[]) {
    const next = mousePositions.map((update) => {
      const resolvedName = playerNameMap.value.get(update.playerNumber) ?? update.name ?? ''
      return {
        playerNumber: update.playerNumber,
        name: resolvedName,
        color: playerColor(update.playerNumber),
        x: Math.max(0, update.x),
        y: Math.max(0, update.y),
      }
    })

    presenceList.value = next
  }

  function detachMousePresence() {
    if (mousePresenceDisposer) {
      mousePresenceDisposer()
      mousePresenceDisposer = null
    }

    if (mousePresence) {
      mousePresence.stop()
      mousePresence = null
    }
  }

  watch([options.hasJoined, options.room], ([joined, room]) => {
    detachMousePresence()

    if (!joined || !room || typeof window === 'undefined') {
      presenceList.value = []
      return
    }

    mousePresence = room.useMousePresence(window)
    syncMousePositions(mousePresence.mousePositions)
    mousePresenceDisposer = mousePresence.listen(syncMousePositions)
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

  onBeforeUnmount(() => {
    detachMousePresence()
  })

  return {
    presenceList,
    visiblePresence,
  }
}
