import { HousePartyError } from '../errors'
import type { GameEvent } from '../generated/events'
import type {
  GameHandle,
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
import { parseGameEvent } from './gameEvents'
import type { Trace } from '../trace'

type JoinContext = {
  name: string
  playerNumber: number
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

    return {
      id: toPlayerId(joinResult.player.number),
      number: joinResult.player.number,
      name: joinResult.player.name,
    }
  }

  async startTurnBasedGame(adminPlayer: PlayerHandle): Promise<GameHandle<TState>> {
    this.trace.log('room', 'Starting turn-based game.', { roomId: this.id, adminPlayerId: adminPlayer.id })
    const gameId = await this.http.startGame(adminPlayer.id)

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

    this.trace.log('room', 'Started turn-based game.', { roomId: this.id, gameId })

    return game
  }

  async stopTurnBasedGame(adminPlayer: PlayerHandle): Promise<void> {
    if (!this.activeGame) {
      return
    }

    this.trace.log('room', 'Stopping turn-based game.', {
      roomId: this.id,
      gameId: this.activeGame.id,
      adminPlayerId: adminPlayer.id,
    })

    await this.http.stopGame(this.activeGame.id, adminPlayer.id)
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
