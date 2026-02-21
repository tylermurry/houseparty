import { HousePartyError } from '../errors'
import type { GameEvent } from '../generated/events'
import type {
  GameHandle,
  NormalizedMouseEvent,
  ParseState,
  PlayerHandle,
  PlayerSummary,
  RoomEvent,
  RoomHandle,
} from '../types'
import { HttpTransport } from '../transport/http'
import { RealtimeTransport } from '../transport/signalr'
import { Emitter } from './emitter'
import { GameHandleImpl } from './game'
import { PlayerHandleImpl } from './player'
import { parseGameEvent } from './gameEvents'
import type { Trace } from '../trace'
import {
  MOUSE_SEND_INTERVAL_MS,
  parseRawMousePresenceUpdate,
  quantizeMousePosition,
  toNormalizedMouseEvent,
} from './mousePresence'

type JoinContext = {
  name: string
  playerNumber: number
}

type MousePresenceSession = {
  playerNumber: number
  cb: (mouseEvent: NormalizedMouseEvent) => void
}

function toPlayerId(number: number): string {
  return `player-${number}`
}

export class RoomHandleImpl<TState> implements RoomHandle<TState> {
  readonly id: string

  private readonly http: HttpTransport
  private readonly parseState: ParseState<TState>
  private readonly trace: Trace

  private readonly roomEventEmitter = new Emitter<RoomEvent>()
  private _players: PlayerSummary[] = []
  private joinContext: JoinContext | null = null
  private realtime: RealtimeTransport | null = null
  private activeGame: GameHandleImpl<TState> | null = null
  private readonly joinedGroups = new Set<string>()
  private readonly mousePresenceSessions = new Set<MousePresenceSession>()
  private readonly mousePresenceDisposers = new Set<() => void>()

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

  get players(): readonly PlayerSummary[] {
    return this._players
  }

  listenForEvents(cb: (event: RoomEvent) => void): () => void {
    return this.roomEventEmitter.on(cb)
  }

  useMousePresence(
    player: PlayerHandle,
    onNormalizedMouseEvent: (mouseEvent: NormalizedMouseEvent) => void,
  ): () => void {
    if (!this.joinContext || !this.realtime) {
      throw new HousePartyError('INVALID_STATE', 'Cannot use mouse presence before joining the room.')
    }

    if (this.joinContext.playerNumber !== player.number) {
      throw new HousePartyError('INVALID_STATE', 'Mouse presence player does not match the joined room identity.')
    }

    if (typeof window === 'undefined') {
      throw new HousePartyError('INVALID_STATE', 'Mouse presence requires a browser window context.')
    }

    let latestPointer: { x: number; y: number } | null = null
    let lastSent: { x: number; y: number } | null = null
    let timerId: number | null = null
    const session: MousePresenceSession = {
      playerNumber: player.number,
      cb: onNormalizedMouseEvent,
    }

    const handlePointerMove = (event: PointerEvent) => {
      latestPointer = { x: event.clientX, y: event.clientY }
    }

    const handlePointerLeave = () => {
      latestPointer = null
    }

    const sendLatestPointer = async () => {
      if (!latestPointer) {
        return
      }

      const quantized = quantizeMousePosition(latestPointer, {
        width: window.innerWidth,
        height: window.innerHeight,
      })
      if (!quantized) {
        return
      }

      if (lastSent && lastSent.x === quantized.x && lastSent.y === quantized.y) {
        return
      }

      lastSent = quantized

      try {
        await this.http.updateMousePresence(this.id, {
          playerNumber: player.number,
          name: player.name.trim().slice(0, 10),
          x: quantized.x,
          y: quantized.y,
        })
      } catch {
        // Ignore transient mouse update failures.
      }
    }

    const dispose = () => {
      if (timerId !== null) {
        window.clearInterval(timerId)
        timerId = null
      }

      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('pointerleave', handlePointerLeave)
      this.mousePresenceSessions.delete(session)
      this.mousePresenceDisposers.delete(dispose)
    }

    this.mousePresenceSessions.add(session)
    this.mousePresenceDisposers.add(dispose)
    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerleave', handlePointerLeave)
    timerId = window.setInterval(() => {
      void sendLatestPointer()
    }, MOUSE_SEND_INTERVAL_MS)

    return dispose
  }

  async join(name: string, requestedPlayerNumber: number | null): Promise<PlayerHandle> {
    this.trace.log('room', 'Joining room group.', {
      roomId: this.id,
      name,
      requestedPlayerNumber,
    })

    await this.ensureRealtimeStarted()

    const joinResult = await this.http.joinRoom(this.id, this.connectionId, name, requestedPlayerNumber)
    this.joinContext = {
      name,
      playerNumber: joinResult.player.number,
    }

    this.joinedGroups.add(this.id)
    this._players = joinResult.players.map((player) => ({
      id: toPlayerId(player.number),
      number: player.number,
      name: player.name,
    }))

    this.roomEventEmitter.emit({
      type: 'playerRosterUpdated',
      players: this._players,
    })

    this.trace.log('room', 'Joined room group.', { roomId: this.id, player: joinResult.player })

    return new PlayerHandleImpl({
      id: toPlayerId(joinResult.player.number),
      number: joinResult.player.number,
      name: joinResult.player.name,
      http: this.http,
      trace: this.trace,
    })
  }

  async createTurnBasedGame(adminPlayer: PlayerHandle, seatCount: number): Promise<GameHandle<TState>> {
    this.trace.log('room', 'Creating turn-based game.', { roomId: this.id, adminPlayerId: adminPlayer.id, seatCount })
    const gameId = await this.http.createGame(adminPlayer.id, seatCount)

    const game = new GameHandleImpl<TState>({
      id: gameId,
      http: this.http,
      parseState: this.parseState,
      trace: this.trace,
    })

    this.activeGame = game

    if (this.joinContext && this.realtime) {
      await this.http.joinRoom(gameId, this.connectionId, this.joinContext.name, this.joinContext.playerNumber)
      this.joinedGroups.add(gameId)
    }

    this.trace.log('room', 'Created turn-based game.', { roomId: this.id, gameId })

    return game
  }

  async endTurnBasedGame(adminPlayer: PlayerHandle): Promise<void> {
    if (!this.activeGame) {
      return
    }

    this.trace.log('room', 'Ending turn-based game.', {
      roomId: this.id,
      gameId: this.activeGame.id,
      adminPlayerId: adminPlayer.id,
    })

    await this.activeGame.end(adminPlayer)
    await this.activeGame.dispose()
    this.activeGame = null
  }

  async dispose(): Promise<void> {
    this.trace.log('room', 'Disposing room handle.', { roomId: this.id })

    if (this.activeGame) {
      await this.activeGame.dispose()
      this.activeGame = null
    }

    if (this.realtime) {
      await this.realtime.stop()
      this.realtime = null
    }

    for (const disposeMousePresence of [...this.mousePresenceDisposers]) {
      disposeMousePresence()
    }

    this.mousePresenceSessions.clear()
    this.mousePresenceDisposers.clear()
    this.roomEventEmitter.clear()
    this.joinedGroups.clear()
  }

  private async ensureRealtimeStarted(): Promise<void> {
    if (this.realtime) {
      return
    }

    const negotiation = await this.http.negotiateSignalR()

    this.realtime = new RealtimeTransport(negotiation, {
      onPlayerRosterUpdated: (payload) => {
        if (!Array.isArray(payload)) {
          return
        }

        const players = payload
          .map((raw) => {
            if (!raw || typeof raw !== 'object') {
              return null
            }

            const value = raw as { number?: number | string; name?: string }
            const number =
              typeof value.number === 'string' ? Number.parseInt(value.number, 10) : value.number ?? Number.NaN

            if (!Number.isFinite(number) || !value.name) {
              return null
            }

            return {
              id: toPlayerId(number),
              number,
              name: value.name,
            } satisfies PlayerSummary
          })
          .filter((player): player is PlayerSummary => player !== null)

        this._players = players
        this.trace.log('room', 'Received player roster update.', { roomId: this.id, playersCount: players.length })
        this.roomEventEmitter.emit({ type: 'playerRosterUpdated', players })
      },
      onMousePresenceUpdated: (payload) => {
        this.trace.log('room', 'Received mouse presence update.', { roomId: this.id })
        this.roomEventEmitter.emit({ type: 'mousePresenceUpdated', payload })

        const parsed = parseRawMousePresenceUpdate(payload)
        if (!parsed) {
          this.trace.traceOnly('room', 'Ignored unparseable mouse presence payload.', { roomId: this.id, payload })
          return
        }

        const normalized = toNormalizedMouseEvent(parsed)
        for (const session of this.mousePresenceSessions) {
          if (session.playerNumber === normalized.playerNumber) {
            continue
          }

          try {
            session.cb(normalized)
          } catch (error) {
            this.trace.error('room', 'Mouse presence callback threw.', {
              roomId: this.id,
              playerNumber: normalized.playerNumber,
              error,
            })
          }
        }
      },
      onGameEvent: (payload) => {
        let parsed: GameEvent
        try {
          parsed = parseGameEvent(payload)
        } catch (error) {
          this.trace.error('room', 'Ignored unparseable game event payload.', { roomId: this.id, error })
          return
        }

        this.trace.log('room', 'Received game event.', { roomId: this.id, eventName: parsed.name, eventPayload: parsed })
        this.roomEventEmitter.emit({ type: 'gameEvent', payload: parsed })
        if (this.activeGame) {
          this.activeGame.ingestRawEvent(parsed)
        }
      },
      onGameStateSnapshot: (payload) => {
        this.trace.log('room', 'Received game state snapshot.', { roomId: this.id })
        this.roomEventEmitter.emit({ type: 'gameStateSnapshot', payload })
        if (this.activeGame) {
          this.activeGame.ingestRawState(payload)
        }
      },
      onReconnected: async (connectionId) => {
        if (!this.joinContext) {
          return
        }

        this.trace.log('room', 'Realtime reconnected. Rejoining groups.', {
          roomId: this.id,
          groups: [...this.joinedGroups],
        })

        for (const groupId of this.joinedGroups) {
          await this.http.joinRoom(groupId, connectionId, this.joinContext.name, this.joinContext.playerNumber)
        }
      },
      onClosed: () => {
        this.trace.log('room', 'Realtime connection closed for room handle.', { roomId: this.id })
      },
    }, this.trace)

    await this.realtime.start()
  }

  private get connectionId(): string {
    if (!this.realtime) {
      throw new HousePartyError('INVALID_STATE', 'Realtime connection has not been started yet.')
    }

    return this.realtime.connectionId
  }
}
