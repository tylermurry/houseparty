import { describe, expect, it, vi } from 'vitest'
import { GameHandleImpl } from './game'
import { PlayerHandleImpl } from './player'

type DemoState = {
  phase: string
  turn: number
}

function createHarness() {
  const http = {
    joinGame: vi.fn(async () => {}),
    startTurn: vi.fn(async () => {}),
    makeMove: vi.fn(async () => {}),
    endTurn: vi.fn(async () => null as string | null),
    startGame: vi.fn(async () => {}),
    endGame: vi.fn(async () => {}),
  }

  const trace = {
    log: vi.fn(),
    error: vi.fn(),
    traceOnly: vi.fn(),
  }

  const game = new GameHandleImpl<DemoState>({
    id: 'game-1',
    http: http as never,
    parseState: (raw) => {
      if (typeof raw === 'string') {
        return JSON.parse(raw) as DemoState
      }

      return raw as DemoState
    },
    trace: trace as never,
  })

  const player1 = new PlayerHandleImpl({
    id: 'player-1',
    number: 1,
    name: 'Player 1',
    http: http as never,
    trace: trace as never,
  })

  const player2 = new PlayerHandleImpl({
    id: 'player-2',
    number: 2,
    name: 'Player 2',
    http: http as never,
    trace: trace as never,
  })

  return { game, http, player1, player2 }
}

describe('PlayerHandleImpl', () => {
  it('joins and makes moves using the player identity', async () => {
    const { game, http, player1 } = createHarness()
    await player1.join(game)
    await player1.startTurn(game)
    await player1.makeMove(game, 'attack:slot-2')

    expect(http.joinGame).toHaveBeenCalledWith('game-1', 'player-1')
    expect(http.startTurn).toHaveBeenCalledWith('game-1', 'player-1')
    expect(http.makeMove).toHaveBeenCalledWith('game-1', 'player-1', 'attack:slot-2')
  })

  it('keeps command identity isolated per player', async () => {
    const { game, http, player1, player2 } = createHarness()
    await player1.startTurn(game)
    await player2.startTurn(game)

    expect(http.startTurn).toHaveBeenNthCalledWith(1, 'game-1', 'player-1')
    expect(http.startTurn).toHaveBeenNthCalledWith(2, 'game-1', 'player-2')
  })

  it('ingests state returned from endTurn into the parent game state', async () => {
    const { game, http, player1 } = createHarness()
    http.endTurn.mockResolvedValueOnce(JSON.stringify({ phase: 'resolve', turn: 3 }))

    await player1.endTurn(game, JSON.stringify({ phase: 'resolve', turn: 2 }))

    expect(http.endTurn).toHaveBeenCalledWith('game-1', 'player-1', JSON.stringify({ phase: 'resolve', turn: 2 }))
    expect(game.state).toEqual({ phase: 'resolve', turn: 3 })
  })
})
