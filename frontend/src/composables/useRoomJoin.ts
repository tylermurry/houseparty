import { ref, type Ref } from 'vue'
import { joinRoom as joinRoomApi, normalizePlayers, toNumber, type PlayerEntry } from '@/services/roomService'

type UseRoomJoinOptions = {
  roomId: Ref<string>
  playerName: Ref<string>
  players: Ref<PlayerEntry[]>
  playerNumber: Ref<number | null>
  hasJoined: Ref<boolean>
}

export function useRoomJoin(options: UseRoomJoinOptions) {
  const isJoining = ref(false)
  const joinError = ref('')

  async function joinRoom(connectionId: string, existingPlayerNumber?: number | null) {
    const trimmedName = options.playerName.value.trim().slice(0, 10)
    if (!trimmedName) {
      joinError.value = 'Name is required.'
      return
    }

    isJoining.value = true
    joinError.value = ''

    try {
      const payload = await joinRoomApi(
        options.roomId.value,
        connectionId,
        trimmedName,
        existingPlayerNumber ?? null,
      )

      const responsePlayer = payload.data?.player
      const responsePlayers = payload.data?.players ?? []
      options.players.value = normalizePlayers(
        responsePlayers.length > 0 ? responsePlayers : responsePlayer ? [responsePlayer] : [],
      )
      options.playerNumber.value = responsePlayer ? toNumber(responsePlayer.number) : options.playerNumber.value
      options.hasJoined.value = true
    } catch (error) {
      joinError.value = error instanceof Error ? error.message : 'Failed to join room.'
    } finally {
      isJoining.value = false
    }
  }

  return {
    isJoining,
    joinError,
    joinRoom,
  }
}
