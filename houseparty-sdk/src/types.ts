import type { GameEvent, GameObjectLock } from './generated/events'
import type { LogLevel, TraceLogger } from './trace'

export type PlayerId = string

export type PlayerSummary = {
  id: PlayerId
  number: number
  name: string
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

export type ListenDisposer = () => void

export interface PlayerHandle {
  readonly id: PlayerId
  readonly number: number
  readonly name: string
}

export interface GameHandle<TState> {
  readonly id: string
  readonly events: readonly GameEvent[]
  readonly state: TState | null
  readonly objects: readonly GameObjectLock[]

  startTurn(player: PlayerHandle): Promise<void>
  makeMove(player: PlayerHandle, movePayload: string): Promise<void>
  endTurn(player: PlayerHandle, statePayload: string): Promise<void>

  onEvent(cb: (event: GameEvent) => void): ListenDisposer
  onStateChange(cb: (state: TState | null) => void): ListenDisposer
  onObjectsChange(cb: (objects: readonly GameObjectLock[]) => void): ListenDisposer

  dispose(): Promise<void>
}

export interface RoomHandle<TState> {
  readonly id: string
  readonly players: readonly PlayerSummary[]

  listenForEvents(cb: (event: RoomEvent) => void): ListenDisposer
  startTurnBasedGame(adminPlayer: PlayerHandle): Promise<GameHandle<TState>>
  dispose(): Promise<void>
}

export interface HousePartyClient<TState> {
  createRoom(): Promise<RoomHandle<TState>>
  joinRoom(
    roomId: string,
    name: string,
    options?: JoinRoomOptions,
  ): Promise<{ room: RoomHandle<TState>; player: PlayerHandle }>
}
