import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { createHousePartyClient, type NormalizedMouseEvent, type RoomHandle, type PlayerHandle } from '@houseparty/sdk'
import type { PlayerEntry } from '@/services/roomService'

type UseMousePresenceOptions = {
  roomId: Ref<string>
  playerName: Ref<string>
  playerNumber: Ref<number | null>
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
  const sdkClient = createHousePartyClient({
    baseUrl: import.meta.env.VITE_BACKEND_API_URL ?? '',
  })
  let mousePresenceDisposer: (() => void) | null = null
  let mousePresenceRoom: RoomHandle<unknown> | null = null

  const playerNameMap = computed(
    () => new Map(options.players.value.map((player) => [player.number, player.name])),
  )
  const visiblePresence = computed(() =>
    presenceList.value.filter((presence) => presence.playerNumber !== options.playerNumber.value),
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

  function disposeMousePresenceRoom() {
    if (mousePresenceDisposer) {
      mousePresenceDisposer()
      mousePresenceDisposer = null
    }

    if (mousePresenceRoom) {
      void mousePresenceRoom.dispose()
      mousePresenceRoom = null
    }
  }

  async function startSdkMousePresenceSession(playerNumber: number) {
    const trimmedName = options.playerName.value.trim().slice(0, 10)
    const roomId = options.roomId.value

    if (!trimmedName || !roomId) {
      return
    }

    const joinResult = await sdkClient.joinRoom(roomId, trimmedName, { playerNumber })
    const room = joinResult.room as RoomHandle<unknown>
    const player = joinResult.player as PlayerHandle
    mousePresenceRoom = room
    mousePresenceDisposer = room.useMousePresence(player, (mouseEvent) => {
      upsertPresence(mouseEvent)
    })
  }

  watch([options.hasJoined, options.playerNumber], ([joined, playerNumber]) => {
    disposeMousePresenceRoom()

    if (!joined || playerNumber === null) {
      return
    }

    void startSdkMousePresenceSession(playerNumber)
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
    disposeMousePresenceRoom()
  })

  return {
    presenceList,
    visiblePresence,
    advancePresence,
  }
}
