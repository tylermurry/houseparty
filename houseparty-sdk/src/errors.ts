export type HousePartyErrorCode =
  | 'NETWORK_ERROR'
  | 'NEGOTIATION_ERROR'
  | 'ROOM_JOIN_ERROR'
  | 'GAME_COMMAND_REJECTED'
  | 'PROJECTION_ERROR'
  | 'INVALID_STATE'

export class HousePartyError extends Error {
  readonly code: HousePartyErrorCode
  readonly cause?: unknown
  readonly context?: Record<string, unknown>

  constructor(
    code: HousePartyErrorCode,
    message: string,
    options?: { cause?: unknown; context?: Record<string, unknown> },
  ) {
    super(message)
    this.name = 'HousePartyError'
    this.code = code
    this.cause = options?.cause
    this.context = options?.context
  }
}
