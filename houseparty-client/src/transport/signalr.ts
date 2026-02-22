import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { HousePartyError } from '../errors'
import type { Trace } from '../trace'

export type RealtimeHandlers = {
  onPlayerRosterUpdated: (players: unknown) => void
  onMousePresenceUpdated: (payload: unknown) => void
  onGameEvent: (payload: unknown) => void
  onGameStateSnapshot: (payload: unknown) => void
  onReconnected: (connectionId: string) => Promise<void> | void
  onClosed: () => void
}

export class RealtimeTransport {
  private readonly hub: HubConnection
  private readonly trace: Trace

  constructor(
    negotiation: { url: string; accessToken: string },
    handlers: RealtimeHandlers,
    trace: Trace,
  ) {
    this.trace = trace

    this.hub = new HubConnectionBuilder()
      .withUrl(negotiation.url, {
        accessTokenFactory: () => negotiation.accessToken,
      })
      .withAutomaticReconnect()
      .build()

    this.hub.on('playerRosterUpdated', handlers.onPlayerRosterUpdated)
    this.hub.on('mousePresenceUpdated', handlers.onMousePresenceUpdated)
    this.hub.on('gameEvent', handlers.onGameEvent)
    this.hub.on('gameStateSnapshot', handlers.onGameStateSnapshot)

    this.hub.onreconnected(async (connectionId) => {
      this.trace.log('realtime', 'Connection re-established.', { connectionId })

      if (!connectionId) {
        return
      }

      await handlers.onReconnected(connectionId)
    })

    this.hub.onclose(() => {
      this.trace.log('realtime', 'Connection closed.')
      handlers.onClosed()
    })
  }

  async start(): Promise<void> {
    this.trace.log('realtime', 'Starting SignalR connection.')
    try {
      await this.hub.start()
      if (!this.hub.connectionId) {
        throw new Error('SignalR connection id missing')
      }

      this.trace.log('realtime', 'SignalR connection started.', { connectionId: this.hub.connectionId })
    } catch (error) {
      this.trace.error('realtime', 'Failed to start SignalR connection.', { error })
      throw new HousePartyError('NEGOTIATION_ERROR', 'Failed to start realtime connection.', {
        cause: error,
      })
    }
  }

  get connectionId(): string {
    const id = this.hub.connectionId
    if (!id) {
      throw new HousePartyError('INVALID_STATE', 'Realtime connection is not established.')
    }

    return id
  }

  async stop(): Promise<void> {
    this.trace.log('realtime', 'Stopping SignalR connection.')
    await this.hub.stop()
  }
}
