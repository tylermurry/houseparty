import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HousePartyError } from '../errors'

type EventHandler = (payload: unknown) => void

const realtimeStopMock = vi.fn(async () => {})
let realtimeEventHandlers = new Map<string, Set<EventHandler>>()

function emitRealtimeEvent(eventName: string, payload: unknown): void {
  for (const handler of realtimeEventHandlers.get(eventName) ?? []) {
    handler(payload)
  }
}

vi.mock('../transport/signalr', () => {
  class RealtimeTransportMock {
    connectionId = 'connection-1'

    constructor() {
      // no-op
    }

    async start(): Promise<void> {}

    async stop(): Promise<void> {
      await realtimeStopMock()
    }

    on(eventName: string, handler: EventHandler): () => void {
      if (!realtimeEventHandlers.has(eventName)) {
        realtimeEventHandlers.set(eventName, new Set())
      }

      realtimeEventHandlers.get(eventName)!.add(handler)
      return () => {
        realtimeEventHandlers.get(eventName)?.delete(handler)
      }
    }

  }

  return { RealtimeTransport: RealtimeTransportMock }
})

import { RoomHandleImpl } from './room'

class FakeWindow {
  innerWidth = 100
  innerHeight = 100

  private readonly listeners = new Map<string, Set<(event: any) => void>>()
  private readonly intervalCallbacks = new Map<number, () => void>()
  private idCounter = 1

  addEventListener(type: string, cb: (event: any) => void): void {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, new Set())
    }

    this.listeners.get(type)!.add(cb)
  }

  removeEventListener(type: string, cb: (event: any) => void): void {
    this.listeners.get(type)?.delete(cb)
  }

  dispatchEvent(type: string, event: any): void {
    for (const cb of this.listeners.get(type) ?? []) {
      cb(event)
    }
  }

  setInterval(cb: () => void): number {
    const id = this.idCounter++
    this.intervalCallbacks.set(id, cb)
    return id
  }

  clearInterval(id: number): void {
    this.intervalCallbacks.delete(id)
  }

  tickIntervals(): void {
    for (const cb of [...this.intervalCallbacks.values()]) {
      cb()
    }
  }

  listenerCount(type: string): number {
    return this.listeners.get(type)?.size ?? 0
  }

  setViewport(width: number, height: number): void {
    this.innerWidth = width
    this.innerHeight = height
    this.dispatchEvent('resize', {})
  }
}

function createHarness() {
  const http = {
    negotiateSignalR: vi.fn(async () => ({ url: 'https://signalr.test', accessToken: 'token' })),
    joinRoom: vi.fn(async () => ({
      player: { number: 1, name: 'Local' },
      players: [{ number: 1, name: 'Local' }],
    })),
    updateMousePresence: vi.fn(async () => {}),
    createGame: vi.fn(),
  }

  const trace = {
    log: vi.fn(),
    error: vi.fn(),
    traceOnly: vi.fn(),
  }

  const room = new RoomHandleImpl<unknown>({
    id: 'room-1',
    http: http as never,
    parseState: (raw) => raw,
    trace: trace as never,
  })

  return { room, http, trace }
}

describe('RoomHandleImpl mouse presence', () => {
  beforeEach(() => {
    realtimeStopMock.mockClear()
    realtimeEventHandlers = new Map()
  })

  it('throws when mouse presence is used before joining', () => {
    const { room } = createHarness()
    const fakeWindow = new FakeWindow()

    expect(() => room.useMousePresence(fakeWindow as unknown as Window)).toThrow(HousePartyError)
  })

  it('quantizes local pointer updates and deduplicates backend mouse updates', async () => {
    const { room, http } = createHarness()
    const fakeWindow = new FakeWindow()
    await room.join('Local', null)

    room.useMousePresence(fakeWindow as unknown as Window)
    fakeWindow.dispatchEvent('pointermove', { clientX: 50, clientY: 50 })

    fakeWindow.tickIntervals()
    fakeWindow.tickIntervals()

    expect(http.updateMousePresence).toHaveBeenCalledTimes(1)
    expect(http.updateMousePresence).toHaveBeenCalledWith('room-1', {
      playerNumber: 1,
      name: 'Local',
      x: 1024,
      y: 1024,
    })
  })

  it('ignores malformed inbound payloads', async () => {
    const { room } = createHarness()
    const fakeWindow = new FakeWindow()

    await room.join('Local', null)
    const presence = room.useMousePresence(fakeWindow as unknown as Window)
    const callback = vi.fn()
    const stopListening = presence.listen(callback)
    callback.mockClear()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 'bad' })

    expect(callback).not.toHaveBeenCalled()
    stopListening()
  })

  it('suppresses local player updates and emits remote positions', async () => {
    const { room } = createHarness()
    const fakeWindow = new FakeWindow()
    const callback = vi.fn()

    await room.join('Local', null)
    const presence = room.useMousePresence(fakeWindow as unknown as Window)
    const stopListening = presence.listen(callback)
    callback.mockClear()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 1, name: 'Local', x: 500, y: 500 })
    expect(callback).not.toHaveBeenCalled()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 2047, y: 2047 })
    expect(callback).toHaveBeenCalled()
    callback.mockClear()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 0, y: 0 })

    const latest = callback.mock.calls.at(-1)?.[0]
    expect(Array.isArray(latest)).toBe(true)
    expect(latest.at(-1)?.playerNumber).toBe(2)
    expect(latest.at(-1)?.x).toBe(0)
    expect(latest.at(-1)?.y).toBe(0)
    stopListening()
  })

  it('rebinds on repeated useMousePresence calls without duplicate listeners', async () => {
    const { room } = createHarness()
    const firstWindow = new FakeWindow()
    const secondWindow = new FakeWindow()

    await room.join('Local', null)
    const firstPresence = room.useMousePresence(firstWindow as unknown as Window)
    const firstCallback = vi.fn()
    const firstStopListening = firstPresence.listen(firstCallback)
    const secondPresence = room.useMousePresence(secondWindow as unknown as Window)
    const secondCallback = vi.fn()
    const secondStopListening = secondPresence.listen(secondCallback)

    expect(firstWindow.listenerCount('pointermove')).toBe(0)
    expect(secondWindow.listenerCount('pointermove')).toBe(1)

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 0, y: 0 })
    expect(firstCallback).toHaveBeenCalled()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 2047, y: 2047 })
    expect(secondCallback).toHaveBeenCalled()
    firstStopListening()
    secondStopListening()
  })

  it('cleans up listeners and resources on dispose', async () => {
    const { room } = createHarness()
    const fakeWindow = new FakeWindow()

    await room.join('Local', null)
    const presence = room.useMousePresence(fakeWindow as unknown as Window)
    const callback = vi.fn()
    const stopListening = presence.listen(callback)
    await room.dispose()

    expect(fakeWindow.listenerCount('pointermove')).toBe(0)
    expect(realtimeStopMock).toHaveBeenCalledTimes(1)

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 2047, y: 2047 })

    expect(callback).toHaveBeenLastCalledWith([])
    stopListening()
  })

  it('recomputes remote pixel positions on window resize', async () => {
    const { room } = createHarness()
    const fakeWindow = new FakeWindow()

    await room.join('Local', null)
    const presence = room.useMousePresence(fakeWindow as unknown as Window)
    const callback = vi.fn()
    const stopListening = presence.listen(callback)
    callback.mockClear()

    emitRealtimeEvent('mousePresenceUpdated', { playerNumber: 2, name: 'Remote', x: 1024, y: 1024 })
    const beforeResize = callback.mock.calls.at(-1)?.[0]?.at(-1)
    expect(beforeResize?.x).toBe(50)
    expect(beforeResize?.y).toBe(50)

    fakeWindow.setViewport(400, 300)
    const afterResize = callback.mock.calls.at(-1)?.[0]?.at(-1)
    expect(afterResize?.x).toBe(200)
    expect(afterResize?.y).toBe(150)

    stopListening()
  })
})
