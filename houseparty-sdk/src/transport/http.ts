import { createClient as createApiClient, createConfig } from '../generated/client/client'
import type { Client } from '../generated/client/client'
import {
  postApiEngineTurnBasedGameCreateGame,
  postApiEngineTurnBasedGameEndTurn,
  postApiEngineTurnBasedGameJoinGame,
  postApiEngineTurnBasedGameMakeMove,
  postApiEngineTurnBasedGameStartGame,
  postApiEngineTurnBasedGameStartTurn,
  postApiEngineTurnBasedGameEndGame,
  postApiRooms,
  postApiRoomsByRoomIdJoin,
  postApiRoomsByRoomIdMouse,
  postApiSignalrNegotiate,
} from '../generated/client'
import type { RoomPlayer } from '../generated/client'
import { HousePartyError } from '../errors'
import type { HousePartyErrorCode } from '../errors'
import type { Trace } from '../trace'

export type JoinedRoomResult = {
  player: { number: number; name: string }
  players: Array<{ number: number; name: string }>
}

function normalizePlayer(player: RoomPlayer): { number: number; name: string } {
  const number = typeof player.number === 'string' ? Number.parseInt(player.number, 10) : player.number
  return {
    number,
    name: player.name,
  }
}

function mapError(code: HousePartyErrorCode, message: string, cause: unknown): never {
  throw new HousePartyError(code, message, { cause })
}

export class HttpTransport {
  private readonly client: Client
  private readonly trace: Trace

  constructor(baseUrl: string, fetchImpl: typeof fetch | undefined, trace: Trace) {
    this.trace = trace
    this.client = createApiClient(createConfig({
      baseUrl: baseUrl as never,
      fetch: fetchImpl,
    }))
  }

  async createRoom(): Promise<string> {
    this.trace.log('http', 'POST /api/rooms')
    try {
      const data = await postApiRooms({
        client: this.client,
        throwOnError: true,
      })

      if (!data.data.id) {
        throw new Error('Create room response missing id')
      }

      this.trace.log('http', 'Created room response.', { roomId: data.data.id })
      return data.data.id
    } catch (error) {
      this.trace.error('http', 'Create room failed.', { error })
      mapError('NETWORK_ERROR', 'Failed to create room.', error)
    }
  }

  async negotiateSignalR(): Promise<{ url: string; accessToken: string }> {
    this.trace.log('http', 'POST /api/signalr/negotiate')
    try {
      const data = await postApiSignalrNegotiate({
        client: this.client,
        throwOnError: true,
      })

      if (!data.data.url || !data.data.accessToken) {
        throw new Error('SignalR negotiation response missing url or accessToken')
      }

      this.trace.log('http', 'Negotiated SignalR connection.')
      return data.data
    } catch (error) {
      this.trace.error('http', 'SignalR negotiate failed.', { error })
      mapError('NEGOTIATION_ERROR', 'Failed to negotiate realtime connection.', error)
    }
  }

  async joinRoom(
    roomId: string,
    connectionId: string,
    name: string,
    playerNumber: number | null,
  ): Promise<JoinedRoomResult> {
    this.trace.log('http', 'POST /api/rooms/{roomId}/join', { roomId, name, playerNumber })
    try {
      const data = await postApiRoomsByRoomIdJoin({
        client: this.client,
        throwOnError: true,
        path: { roomId },
        body: {
          connectionId,
          name,
          playerNumber,
        },
      })

      const result = {
        player: normalizePlayer(data.data.player),
        players: data.data.players.map(normalizePlayer),
      }

      this.trace.log('http', 'Joined room response.', { roomId, player: result.player })
      return result
    } catch (error) {
      this.trace.error('http', 'Join room failed.', { roomId, error })
      mapError('ROOM_JOIN_ERROR', `Failed to join room '${roomId}'.`, error)
    }
  }

  async createGame(playerId: string, seatCount: number): Promise<string> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/create-game', { playerId, seatCount })
    try {
      const data = await postApiEngineTurnBasedGameCreateGame({
        client: this.client,
        throwOnError: true,
        body: { playerId, seatCount },
      })

      if (!data.data.gameCreated || !data.data.gameId) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not create game.')
      }

      this.trace.log('http', 'Create game accepted.', { gameId: data.data.gameId })
      return data.data.gameId
    } catch (error) {
      this.trace.error('http', 'Create game failed.', { error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to create game.', error)
    }
  }

  async updateMousePresence(
    roomId: string,
    payload: { playerNumber: number; name: string; x: number; y: number },
  ): Promise<void> {
    this.trace.traceOnly('http', 'POST /api/rooms/{roomId}/mouse', { roomId, payload })
    try {
      await postApiRoomsByRoomIdMouse({
        client: this.client,
        throwOnError: true,
        path: { roomId },
        body: payload,
      })
    } catch (error) {
      this.trace.traceOnly('http', 'Mouse presence update failed.', { roomId, error })
      mapError('NETWORK_ERROR', `Failed to update mouse presence for room '${roomId}'.`, error)
    }
  }

  async joinGame(gameId: string, playerId: string): Promise<void> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/join-game', { gameId, playerId })
    try {
      const data = await postApiEngineTurnBasedGameJoinGame({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId },
      })

      if (!data.data.joined) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not join game.')
      }

      this.trace.log('http', 'Join game accepted.', { gameId, playerId })
    } catch (error) {
      this.trace.error('http', 'Join game failed.', { gameId, playerId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to join game.', error)
    }
  }

  async startGame(gameId: string, playerId: string): Promise<void> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/start-game', { gameId, playerId })
    try {
      const data = await postApiEngineTurnBasedGameStartGame({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId },
      })

      if (!data.data.gameStarted) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not start game.')
      }

      this.trace.log('http', 'Start game accepted.', { gameId, playerId })
    } catch (error) {
      this.trace.error('http', 'Start game failed.', { gameId, playerId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to start game.', error)
    }
  }

  async endGame(gameId: string, playerId: string): Promise<void> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/end-game', { gameId, playerId })
    try {
      const data = await postApiEngineTurnBasedGameEndGame({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId },
      })

      if (!data.data.gameEnded) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not end game.')
      }

      this.trace.log('http', 'End game accepted.', { gameId })
    } catch (error) {
      this.trace.error('http', 'End game failed.', { gameId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to end game.', error)
    }
  }

  async startTurn(gameId: string, playerId: string): Promise<void> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/start-turn', { gameId, playerId })
    try {
      const data = await postApiEngineTurnBasedGameStartTurn({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId },
      })

      if (!data.data.turnStarted) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not start turn.')
      }

      this.trace.log('http', 'Start turn accepted.', { gameId, playerId })
    } catch (error) {
      this.trace.error('http', 'Start turn failed.', { gameId, playerId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to start turn.', error)
    }
  }

  async makeMove(gameId: string, playerId: string, movePayload: string): Promise<void> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/make-move', { gameId, playerId, movePayload })
    try {
      const data = await postApiEngineTurnBasedGameMakeMove({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId, movePayload },
      })

      if (!data.data.moveAccepted) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not make move.')
      }

      this.trace.log('http', 'Make move accepted.', { gameId, playerId })
    } catch (error) {
      this.trace.error('http', 'Make move failed.', { gameId, playerId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to make move.', error)
    }
  }

  async endTurn(gameId: string, playerId: string, statePayload: string): Promise<string | null> {
    this.trace.log('http', 'POST /api/engine/turn-based-game/end-turn', { gameId, playerId })
    try {
      const data = await postApiEngineTurnBasedGameEndTurn({
        client: this.client,
        throwOnError: true,
        body: { gameId, playerId, statePayload },
      })

      if (!data.data.turnEnded) {
        throw new HousePartyError('GAME_COMMAND_REJECTED', data.data.errorMessage ?? 'Could not end turn.')
      }

      this.trace.log('http', 'End turn accepted.', { gameId, playerId })
      return data.data.statePayload ?? null
    } catch (error) {
      this.trace.error('http', 'End turn failed.', { gameId, playerId, error })
      if (error instanceof HousePartyError) throw error
      mapError('GAME_COMMAND_REJECTED', 'Failed to end turn.', error)
    }
  }
}
