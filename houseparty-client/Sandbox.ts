import { createHousePartyClient } from './src/index'

type DemoState = {
  phase: string
  turn: number
}

async function main() {
  const hostClient = createHousePartyClient<DemoState>({
    baseUrl: 'http://localhost:5407',
    logLevel: 'error',
  })
  const guestClient = createHousePartyClient<DemoState>({
    baseUrl: 'http://localhost:5407',
    logLevel: 'error',
  })

  // Create and Join Room
  const room = await hostClient.createRoom()
  const { player: player1 } = await hostClient.joinRoom(room.id, 'Player 1')
  const { player: player2 } = await guestClient.joinRoom(room.id, 'Player 2')

  // Create, Join and Start Game
  const game = await room.createTurnBasedGame(player1, 2)
  await player1.join(game)
  await player2.join(game)
  await game.start(player1)

  game.onEvent(event => console.log('Game Event:', event))
  game.onStateChange(state => console.log('Game State:', state))

  // Player 1 Turn
  await player1.startTurn(game)
  await player1.makeMove(game, 'attack:slot-1')
  await player1.makeMove(game, 'attack:slot-2')
  await player1.endTurn(game, JSON.stringify({ phase: 'resolve', turn: 1 }))

  // End Game
  await game.end(player1);
  await game.dispose()
  await room.dispose()
}

main().catch((error) => {
  console.error(error)
})
