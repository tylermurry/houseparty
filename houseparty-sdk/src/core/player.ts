import { HousePartyError } from '../errors'
import type { GameHandle, PlayerHandle } from '../types'
import type { HttpTransport } from '../transport/http'
import type { Trace } from '../trace'
import { GameHandleImpl } from './game'

export class PlayerHandleImpl implements PlayerHandle {
  readonly id: string
  readonly number: number
  readonly name: string

  private readonly http: HttpTransport
  private readonly trace: Trace

  constructor(options: {
    id: string
    number: number
    name: string
    http: HttpTransport
    trace: Trace
  }) {
    this.id = options.id
    this.number = options.number
    this.name = options.name
    this.http = options.http
    this.trace = options.trace
  }

  async join<TState>(game: GameHandle<TState>): Promise<void> {
    this.trace.log('game', 'Joining turn-based game.', { gameId: game.id, playerId: this.id })
    await this.http.joinGame(game.id, this.id)
  }

  async startTurn<TState>(game: GameHandle<TState>): Promise<void> {
    this.trace.log('game', 'Starting turn.', { gameId: game.id, playerId: this.id })
    await this.http.startTurn(game.id, this.id)
  }

  async makeMove<TState>(game: GameHandle<TState>, movePayload: string): Promise<void> {
    this.trace.log('game', 'Making move.', { gameId: game.id, playerId: this.id, movePayload })
    await this.http.makeMove(game.id, this.id, movePayload)
  }

  async endTurn<TState>(game: GameHandle<TState>, statePayload: string): Promise<void> {
    this.trace.log('game', 'Ending turn.', { gameId: game.id, playerId: this.id })
    const maybeState = await this.http.endTurn(game.id, this.id, statePayload)

    if (maybeState !== null) {
      this.asGameHandleImpl(game).ingestRawState(maybeState)
    }
  }

  private asGameHandleImpl<TState>(game: GameHandle<TState>): GameHandleImpl<TState> {
    if (game instanceof GameHandleImpl) {
      return game
    }

    throw new HousePartyError('INVALID_STATE', 'Unsupported GameHandle implementation.')
  }
}
