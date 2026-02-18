import type {
  HousePartyClient,
  HousePartyClientOptions,
  JoinRoomOptions,
  ParseState,
} from '../types'
import { HttpTransport } from '../transport/http'
import { RoomHandleImpl } from './room'
import { Trace } from '../trace'

const parseStateDefault = <TState>(state: unknown): TState => {
  if (typeof state === 'string') {
    try {
      return JSON.parse(state) as TState
    } catch {
      return state as TState
    }
  }

  return state as TState
}

export class HousePartyClientImpl<TState> implements HousePartyClient<TState> {
  private readonly http: HttpTransport
  private readonly parseState: ParseState<TState>
  private readonly trace: Trace

  constructor(options: HousePartyClientOptions<TState>) {
    this.trace = new Trace({
      logLevel: options.logLevel,
      logger: options.logger,
    })
    this.http = new HttpTransport(options.baseUrl, options.fetch, this.trace)
    this.parseState = options.parseState ?? parseStateDefault<TState>

    this.trace.log('client', 'Initialized HouseParty client.', {
      baseUrl: options.baseUrl,
    })
  }

  async createRoom() {
    this.trace.log('client', 'Creating room.')
    const roomId = await this.http.createRoom()

    this.trace.log('client', 'Created room.', { roomId })

    return new RoomHandleImpl<TState>({
      id: roomId,
      http: this.http,
      parseState: this.parseState,
      trace: this.trace,
    })
  }

  async joinRoom(roomId: string, name: string, options?: JoinRoomOptions) {
    this.trace.log('client', 'Joining room.', { roomId, name, options })

    const room = new RoomHandleImpl<TState>({
      id: roomId,
      http: this.http,
      parseState: this.parseState,
      trace: this.trace,
    })

    const player = await room.join(name, options?.playerNumber ?? null)

    this.trace.log('client', 'Joined room.', { roomId, player })

    return player
  }
}

export function createHousePartyClient<TState = unknown>(
  options: HousePartyClientOptions<TState>,
): HousePartyClient<TState> {
  return new HousePartyClientImpl(options)
}
