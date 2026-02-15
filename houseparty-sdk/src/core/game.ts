import { HousePartyError } from '../errors'
import type { GameEvent, GameObjectLock } from '../generated/events'
import type { GameHandle, ParseState, PlayerHandle } from '../types'
import { Emitter } from './emitter'
import { parseGameEvent, projectObjectLocks } from './gameEvents'
import { HttpTransport } from '../transport/http'
import type { Trace } from '../trace'

export class GameHandleImpl<TState> implements GameHandle<TState> {
  readonly id: string

  private readonly http: HttpTransport
  private readonly parseState: ParseState<TState>
  private readonly trace: Trace

  private readonly eventEmitter = new Emitter<GameEvent>()
  private readonly stateEmitter = new Emitter<TState | null>()
  private readonly objectsEmitter = new Emitter<readonly GameObjectLock[]>()

  private _events: GameEvent[] = []
  private _state: TState | null = null
  private _objects: readonly GameObjectLock[] = []

  constructor(options: {
    id: string
    http: HttpTransport
    parseState: ParseState<TState>
    trace: Trace
  }) {
    this.id = options.id
    this.http = options.http
    this.parseState = options.parseState
    this.trace = options.trace
  }

  get events(): readonly GameEvent[] {
    return this._events
  }

  get state(): TState | null {
    return this._state
  }

  get objects(): readonly GameObjectLock[] {
    return this._objects
  }

  async startTurn(player: PlayerHandle): Promise<void> {
    this.trace.log('game', 'Starting turn.', { gameId: this.id, playerId: player.id })
    await this.http.startTurn(this.id, player.id)
  }

  async makeMove(player: PlayerHandle, movePayload: string): Promise<void> {
    this.trace.log('game', 'Making move.', { gameId: this.id, playerId: player.id, movePayload })
    await this.http.makeMove(this.id, player.id, movePayload)
  }

  async endTurn(player: PlayerHandle, statePayload: string): Promise<void> {
    this.trace.log('game', 'Ending turn.', { gameId: this.id, playerId: player.id })
    const maybeState = await this.http.endTurn(this.id, player.id, statePayload)

    if (maybeState !== null) {
      this.ingestRawState(maybeState)
    }
  }

  onEvent(cb: (event: GameEvent) => void): () => void {
    return this.eventEmitter.on(cb)
  }

  onStateChange(cb: (state: TState | null) => void): () => void {
    return this.stateEmitter.on(cb)
  }

  onObjectsChange(cb: (objects: readonly GameObjectLock[]) => void): () => void {
    return this.objectsEmitter.on(cb)
  }

  ingestRawEvent(rawEvent: unknown): void {
    try {
      const event = parseGameEvent(rawEvent)
      this._events = [...this._events, event]
      this.trace.traceOnly('game', 'Ingested game event.', {
        gameId: this.id,
        eventName: event.name,
        totalEvents: this._events.length,
        eventPayload: event,
      })
      this.eventEmitter.emit(event)
      this.rebuildObjects()
    } catch (error) {
      if (error instanceof HousePartyError) {
        throw error
      }

      throw new HousePartyError('PROJECTION_ERROR', 'Failed to process game event.', {
        cause: error,
      })
    }
  }

  ingestRawState(rawState: unknown): void {
    try {
      const parsedState = this.parseState(rawState)
      this._state = parsedState
      this.trace.log('game', 'Ingested game state.', {
        gameId: this.id,
        statePayload: parsedState,
      })
      this.stateEmitter.emit(parsedState)
    } catch (error) {
      throw new HousePartyError('PROJECTION_ERROR', 'Failed to process game state.', {
        cause: error,
      })
    }
  }

  async dispose(): Promise<void> {
    this.trace.log('game', 'Disposing game handle.', { gameId: this.id })
    this.eventEmitter.clear()
    this.stateEmitter.clear()
    this.objectsEmitter.clear()
  }

  private rebuildObjects(): void {
    this._objects = projectObjectLocks(this._events)
    this.trace.log('game', 'Rebuilt object locks.', { gameId: this.id, objectsCount: this._objects.length })
    this.objectsEmitter.emit(this._objects)
  }
}
