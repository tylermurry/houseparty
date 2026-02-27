export { createHousePartyClient } from './core/client'
export { HousePartyError } from './errors'

export type {
  GameEvent,
  GameEventBase,
  GameEventName,
  GameObjectLock,
  ClaimRoleEvent,
  ControlObjectEvent,
  ReleaseObjectEvent,
  ReleaseRoleEvent,
  RevokeObjectEvent,
  RevokeRoleEvent,
  SubmitActionEvent,
} from './generated/events'

export type { LogLevel, LogSeverity, TraceLogger, TraceType } from './trace'

export type {
  GameHandle,
  HousePartyClient,
  HousePartyClientOptions,
  JoinRoomOptions,
  JoinRoomResult,
  ListenDisposer,
  MousePosition,
  MousePresenceHandle,
  NormalizedMouseEvent,
  PlayerHandle,
  PlayerSummary,
  RoomEvent,
  RoomHandle,
} from './types'
