import { onBeforeUnmount, ref, type Ref } from 'vue'
import type { PlayerHandle, PlayerSummary, RoomHandle } from '@houseparty/sdk'
import { housepartyClient } from '@/services/housepartySdkService'
import type { PlayerEntry } from '@/types/player'

export type RoomSession = {
  room: Ref<RoomHandle<unknown> | null>
  player: Ref<PlayerHandle | null>
  playerNumber: Ref<number | null>
  players: Ref<PlayerEntry[]>
  hasJoined: Ref<boolean>
  isJoining: Ref<boolean>
  joinError: Ref<string>
  joinRoom: (name: string) => Promise<void>
  dispose: () => Promise<void>
}

function toPlayerEntries(players: readonly PlayerSummary[]): PlayerEntry[] {
  return players.map((entry) => ({
    number: entry.number,
    name: entry.name,
  }))
}

export function useRoomSession(roomId: Ref<string>): RoomSession {
  const room = ref<RoomHandle<unknown> | null>(null)
  const player = ref<PlayerHandle | null>(null)
  const playerNumber = ref<number | null>(null)
  const players = ref<PlayerEntry[]>([])
  const hasJoined = ref(false)
  const isJoining = ref(false)
  const joinError = ref('')
  let roomEventsDisposer: (() => void) | null = null

  async function dispose() {
    if (roomEventsDisposer) {
      roomEventsDisposer()
      roomEventsDisposer = null
    }

    if (room.value) {
      await room.value.dispose()
    }

    room.value = null
    player.value = null
    playerNumber.value = null
    players.value = []
    hasJoined.value = false
  }

  async function joinRoom(name: string) {
    const trimmedName = name.trim().slice(0, 10)
    if (!trimmedName) {
      joinError.value = 'Name is required.'
      return
    }

    if (!roomId.value) {
      joinError.value = 'Room id is required.'
      return
    }

    isJoining.value = true
    joinError.value = ''

    try {
      await dispose()

      const joinResult = await housepartyClient.joinRoom(roomId.value, trimmedName)
      room.value = joinResult.room as RoomHandle<unknown>
      player.value = joinResult.player
      playerNumber.value = joinResult.player.number
      hasJoined.value = true
      players.value = toPlayerEntries(joinResult.room.players)

      roomEventsDisposer = joinResult.room.listenForEvents((event) => {
        if (event.type === 'playerRosterUpdated') {
          players.value = toPlayerEntries(event.players)
        }
      })
    } catch (error) {
      hasJoined.value = false
      joinError.value = error instanceof Error ? error.message : 'Failed to join room.'
    } finally {
      isJoining.value = false
    }
  }

  onBeforeUnmount(() => {
    void dispose()
  })

  return {
    room,
    player,
    playerNumber,
    players,
    hasJoined,
    isJoining,
    joinError,
    joinRoom,
    dispose,
  }
}
