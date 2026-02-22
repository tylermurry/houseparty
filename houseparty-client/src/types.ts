import type { GameEvent, GameObjectLock } from './generated/events'
import type { LogLevel, TraceLogger } from './trace'

export type PlayerId = string

export type PlayerSummary = {
  id: PlayerId
  number: number
  name: string
}

export type NormalizedMouseEvent = {
  playerId: PlayerId
  playerNumber: number
  name: string
  normalizedX: number
  normalizedY: number
}

export type RoomEvent =
  | { type: 'playerRosterUpdated'; players: readonly PlayerSummary[] }
  | { type: 'mousePresenceUpdated'; payload: unknown }
  | { type: 'gameEvent'; payload: GameEvent }
  | { type: 'gameStateSnapshot'; payload: unknown }

export type ParseState<TState> = (rawState: unknown) => TState

export type HousePartyClientOptions<TState> = {
  baseUrl: string
  parseState?: ParseState<TState>
  fetch?: typeof fetch
  logLevel?: LogLevel
  logger?: TraceLogger
}

export type JoinRoomOptions = {
  playerNumber?: number | null
}

export type JoinRoomResult<TState> = {
  room: RoomHandle<TState>
  player: PlayerHandle
}

export type ListenDisposer = () => void

export interface PlayerHandle {
  readonly id: PlayerId
  readonly number: number
  readonly name: string

  join<TState>(game: GameHandle<TState>): Promise<void>
  startTurn<TState>(game: GameHandle<TState>): Promise<void>
  makeMove<TState>(game: GameHandle<TState>, movePayload: string): Promise<void>
  endTurn<TState>(game: GameHandle<TState>, statePayload: string): Promise<void>
}

export interface GameHandle<TState> {
  readonly id: string
  readonly events: readonly GameEvent[]
  readonly state: TState | null
  readonly objects: readonly GameObjectLock[]

  start(adminPlayer: PlayerHandle): Promise<void>
  end(adminPlayer: PlayerHandle): Promise<void>

  onEvent(cb: (event: GameEvent) => void): ListenDisposer
  onStateChange(cb: (state: TState | null) => void): ListenDisposer
  onObjectsChange(cb: (objects: readonly GameObjectLock[]) => void): ListenDisposer

  dispose(): Promise<void>
}

export interface RoomHandle<TState> {
  readonly id: string
  readonly players: readonly PlayerSummary[]

  listenForEvents(cb: (event: RoomEvent) => void): ListenDisposer
  useMousePresence(
    player: PlayerHandle,
    onNormalizedMouseEvent: (mouseEvent: NormalizedMouseEvent) => void,
  ): ListenDisposer
  createTurnBasedGame(adminPlayer: PlayerHandle, seatCount: number): Promise<GameHandle<TState>>
  dispose(): Promise<void>
}

export interface HousePartyClient<TState> {
  createRoom(): Promise<RoomHandle<TState>>
  joinRoom(
    roomId: string,
    name: string,
    options?: JoinRoomOptions,
  ): Promise<JoinRoomResult<TState>>
}
