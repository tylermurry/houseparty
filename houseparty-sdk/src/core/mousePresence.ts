import type { NormalizedMouseEvent, PlayerId } from '../types'

export const MOUSE_GRID_SIZE = 2048
export const MOUSE_SEND_INTERVAL_MS = 1000 / 30

export type QuantizedMousePosition = {
  x: number
  y: number
}

export type RawMousePresenceUpdate =
  | { playerNumber: number; name: string; x: number; y: number }
  | { PlayerNumber?: string | number; Name?: string; X?: string | number; Y?: string | number }

type ParsedMousePresenceUpdate = {
  playerNumber: number
  name: string
  x: number
  y: number
}

function toFiniteNumber(value: string | number | undefined): number {
  if (typeof value === 'number') {
    return value
  }
  if (typeof value === 'string' && value.length > 0) {
    return Number(value)
  }
  return Number.NaN
}

export function quantizeMousePosition(
  pointer: { x: number; y: number },
  viewport: { width: number; height: number },
): QuantizedMousePosition | null {
  if (viewport.width <= 0 || viewport.height <= 0) {
    return null
  }

  const normalizedX = Math.min(1, Math.max(0, pointer.x / viewport.width))
  const normalizedY = Math.min(1, Math.max(0, pointer.y / viewport.height))
  const x = Math.min(MOUSE_GRID_SIZE - 1, Math.max(0, Math.floor(normalizedX * MOUSE_GRID_SIZE)))
  const y = Math.min(MOUSE_GRID_SIZE - 1, Math.max(0, Math.floor(normalizedY * MOUSE_GRID_SIZE)))

  return { x, y }
}

export function parseRawMousePresenceUpdate(payload: unknown): ParsedMousePresenceUpdate | null {
  if (!payload || typeof payload !== 'object') {
    return null
  }

  const raw = payload as RawMousePresenceUpdate
  const playerNumber = toFiniteNumber('playerNumber' in raw ? raw.playerNumber : raw.PlayerNumber)
  const nameValue = 'name' in raw ? raw.name : raw.Name
  const x = toFiniteNumber('x' in raw ? raw.x : raw.X)
  const y = toFiniteNumber('y' in raw ? raw.y : raw.Y)

  if (!Number.isFinite(playerNumber) || !Number.isFinite(x) || !Number.isFinite(y)) {
    return null
  }

  return {
    playerNumber,
    name: typeof nameValue === 'string' ? nameValue : '',
    x,
    y,
  }
}

export function toNormalizedMouseEvent(
  payload: ParsedMousePresenceUpdate,
): NormalizedMouseEvent {
  const max = MOUSE_GRID_SIZE - 1

  return {
    playerId: (`player-${payload.playerNumber}` as PlayerId),
    playerNumber: payload.playerNumber,
    name: payload.name,
    normalizedX: Math.min(1, Math.max(0, payload.x / max)),
    normalizedY: Math.min(1, Math.max(0, payload.y / max)),
  }
}
