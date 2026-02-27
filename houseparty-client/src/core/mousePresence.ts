import type { MousePosition, MousePresenceHandle, NormalizedMouseEvent, PlayerId } from '../types'
import { Emitter } from './emitter'

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

export function clampNormalizedCoordinate(value: number): number {
  return Math.min(1, Math.max(0, value))
}

type MousePresenceSnapshot = {
  playerId: PlayerId
  playerNumber: number
  name: string
  normalizedX: number
  normalizedY: number
  x: number
  y: number
}

type MousePresenceSessionOptions = {
  roomId: string
  target: Window
  playerNumber: number
  playerName: string
  publishMousePresence: (
    roomId: string,
    payload: { playerNumber: number; name: string; x: number; y: number },
  ) => Promise<void>
  subscribeToRealtimeEvent: (eventName: string, handler: (payload: unknown) => void) => () => void
  onError: (message: string, data?: unknown) => void
}

class MousePresenceSession {
  private readonly roomId: string
  private readonly target: Window
  private readonly playerNumber: number
  private readonly playerName: string
  private readonly publishMousePresence: MousePresenceSessionOptions['publishMousePresence']
  private readonly subscribeToRealtimeEvent: MousePresenceSessionOptions['subscribeToRealtimeEvent']
  private readonly onError: MousePresenceSessionOptions['onError']

  private isDisposed = false
  private latestPointer: { x: number; y: number } | null = null
  private lastSent: { x: number; y: number } | null = null
  private sendTimerId: number | null = null
  private realtimeMousePresenceDisposer: (() => void) | null = null
  private readonly remoteSnapshots = new Map<number, MousePresenceSnapshot>()
  private readonly presenceEmitter = new Emitter<readonly MousePosition[]>()
  private mousePositionsValue: readonly MousePosition[] = []

  readonly handle: MousePresenceHandle

  constructor(options: MousePresenceSessionOptions) {
    this.roomId = options.roomId
    this.target = options.target
    this.playerNumber = options.playerNumber
    this.playerName = options.playerName
    this.publishMousePresence = options.publishMousePresence
    this.subscribeToRealtimeEvent = options.subscribeToRealtimeEvent
    this.onError = options.onError
    const getMousePositions = () => this.mousePositions
    this.handle = {
      get mousePositions() {
        return getMousePositions()
      },
      listen: (cb) => this.listen(cb),
      stop: () => this.stop(),
    }
    this.target.addEventListener('pointermove', this.handlePointerMove)
    this.target.addEventListener('pointerleave', this.handlePointerLeave)
    this.target.addEventListener('resize', this.handleResize)
    this.realtimeMousePresenceDisposer = this.subscribeToRealtimeEvent('mousePresenceUpdated', (payload) => {
      const parsed = parseRawMousePresenceUpdate(payload)
      if (!parsed) {
        return
      }

      this.onRemoteMousePresence(toNormalizedMouseEvent(parsed))
    })
    this.sendTimerId = this.target.setInterval(() => {
      void this.sendLatestPointer()
    }, MOUSE_SEND_INTERVAL_MS)
    this.recomputePixelPositions()
    this.emitMousePositions()
  }

  get mousePositions(): readonly MousePosition[] {
    return this.mousePositionsValue
  }

  listen(cb: (mousePositions: readonly MousePosition[]) => void): () => void {
    cb(this.mousePositionsValue)
    return this.presenceEmitter.on(cb)
  }

  stop(): void {
    this.detach()
    this.remoteSnapshots.clear()
    this.emitMousePositions()
  }

  onRemoteMousePresence(normalized: NormalizedMouseEvent): void {
    if (this.isDisposed || normalized.playerNumber === this.playerNumber) {
      return
    }

    const targetX = clampNormalizedCoordinate(normalized.normalizedX)
    const targetY = clampNormalizedCoordinate(normalized.normalizedY)
    const existing = this.remoteSnapshots.get(normalized.playerNumber)

    if (!existing) {
      this.remoteSnapshots.set(normalized.playerNumber, {
        playerId: normalized.playerId,
        playerNumber: normalized.playerNumber,
        name: normalized.name,
        normalizedX: targetX,
        normalizedY: targetY,
        x: 0,
        y: 0,
      })
    } else {
      existing.playerId = normalized.playerId
      existing.name = normalized.name
      existing.normalizedX = targetX
      existing.normalizedY = targetY
    }

    this.recomputePixelPositions()
    this.emitMousePositions()
  }

  dispose(): void {
    if (this.isDisposed) {
      return
    }
    this.isDisposed = true
    this.stop()
    this.presenceEmitter.clear()
  }

  private readonly handlePointerMove = (event: PointerEvent): void => {
    this.latestPointer = { x: event.clientX, y: event.clientY }
  }

  private readonly handlePointerLeave = (): void => {
    this.latestPointer = null
  }

  private readonly handleResize = (): void => {
    if (this.isDisposed) {
      return
    }

    this.recomputePixelPositions()
    this.emitMousePositions()
  }

  private async sendLatestPointer(): Promise<void> {
    if (this.isDisposed || !this.latestPointer) {
      return
    }

    const quantized = quantizeMousePosition(this.latestPointer, {
      width: this.target.innerWidth,
      height: this.target.innerHeight,
    })
    if (!quantized) {
      return
    }

    if (this.lastSent && this.lastSent.x === quantized.x && this.lastSent.y === quantized.y) {
      return
    }

    this.lastSent = quantized

    try {
      await this.publishMousePresence(this.roomId, {
        playerNumber: this.playerNumber,
        name: this.playerName.trim().slice(0, 10),
        x: quantized.x,
        y: quantized.y,
      })
    } catch {
      // Ignore transient mouse update failures.
    }
  }

  private emitMousePositions(): void {
    this.mousePositionsValue = [...this.remoteSnapshots.values()]
      .sort((a, b) => a.playerNumber - b.playerNumber)
      .map((snapshot) => ({
        playerId: snapshot.playerId,
        playerNumber: snapshot.playerNumber,
        name: snapshot.name,
        x: snapshot.x,
        y: snapshot.y,
      }))

    try {
      this.presenceEmitter.emit(this.mousePositionsValue)
    } catch (error) {
      this.onError('Mouse presence listener threw.', { error })
    }
  }

  private detach(): void {
    if (this.sendTimerId !== null) {
      this.target.clearInterval(this.sendTimerId)
      this.sendTimerId = null
    }
    if (this.realtimeMousePresenceDisposer) {
      this.realtimeMousePresenceDisposer()
      this.realtimeMousePresenceDisposer = null
    }

    this.target.removeEventListener('pointermove', this.handlePointerMove)
    this.target.removeEventListener('pointerleave', this.handlePointerLeave)
    this.target.removeEventListener('resize', this.handleResize)
    this.latestPointer = null
    this.lastSent = null
  }

  private recomputePixelPositions(): void {
    if (this.isDisposed) {
      return
    }

    const width = Math.max(0, this.target.innerWidth)
    const height = Math.max(0, this.target.innerHeight)

    for (const snapshot of this.remoteSnapshots.values()) {
      snapshot.x = Math.round(snapshot.normalizedX * width)
      snapshot.y = Math.round(snapshot.normalizedY * height)
    }
  }
}

export function createMousePresenceSession(options: MousePresenceSessionOptions): {
  handle: MousePresenceHandle
  dispose: () => void
} {
  const session = new MousePresenceSession(options)
  return {
    handle: session.handle,
    dispose: () => session.dispose(),
  }
}
