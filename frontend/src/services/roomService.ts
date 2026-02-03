import API from '@/api-client/client'
import type { RoomPlayer } from '@/api-client/client/types.gen'

export type PlayerEntry = { number: number; name: string }
export type RawPlayer = RoomPlayer | { Number?: string | number; Name?: string }

export function toNumber(value: string | number | undefined | null) {
  if (typeof value === 'number') {
    return value
  }
  if (typeof value === 'string') {
    return Number.parseInt(value, 10)
  }
  return Number.NaN
}

export function normalizePlayers(rawPlayers: RawPlayer[]): PlayerEntry[] {
  return rawPlayers
    .map((player) => ({
      number: toNumber('number' in player ? player.number : player.Number),
      name: ('name' in player ? player.name : player.Name) ?? '',
    }))
    .filter((player) => Number.isFinite(player.number) && player.name.length > 0)
}

export async function createRoom() {
  return API.postApiRooms()
}

export async function joinRoom(
  roomId: string,
  connectionId: string,
  name: string,
  playerNumber: number | null,
) {
  return API.postApiRoomsByRoomIdJoin({
    path: { roomId },
    body: {
      connectionId,
      name,
      playerNumber,
    },
  })
}
