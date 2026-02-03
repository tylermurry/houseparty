import { onBeforeUnmount, ref, shallowRef, type Ref } from 'vue'
import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { negotiateConnection } from '@/services/signalrService'
import { normalizePlayers, type PlayerEntry, type RawPlayer } from '@/services/roomService'

type UseRoomConnectionOptions = {
  onReconnected?: (connectionId: string) => Promise<void> | void
  players?: Ref<PlayerEntry[]>
}

export function useRoomConnection(options: UseRoomConnectionOptions = {}) {
  const connection = shallowRef<HubConnection | null>(null)
  const isConnected = ref(false)
  const isConnecting = ref(true)
  const connectionError = ref('')
  const players = options.players ?? ref<PlayerEntry[]>([])

  async function startConnection() {
    isConnecting.value = true
    connectionError.value = ''

    const negotiation = await negotiateConnection()

    const hub = new HubConnectionBuilder()
      .withUrl(negotiation.url, {
        accessTokenFactory: () => negotiation.accessToken,
      })
      .withAutomaticReconnect()
      .build()

    hub.on('playerRosterUpdated', (updatedPlayers: RawPlayer[]) => {
      const normalized = normalizePlayers(updatedPlayers)
      if (normalized.length > 0) {
        players.value = normalized
      }
    })

    hub.onreconnected(async (connectionId) => {
      isConnected.value = true
      if (connectionId && options.onReconnected) {
        await options.onReconnected(connectionId)
      }
    })

    hub.onclose(() => {
      isConnected.value = false
    })

    connection.value = hub

    try {
      await hub.start()
      isConnected.value = true
      if (!hub.connectionId) {
        throw new Error('SignalR connection id missing.')
      }
    } catch (error) {
      isConnected.value = false
      connectionError.value = error instanceof Error ? error.message : 'Failed to connect.'
    } finally {
      isConnecting.value = false
    }
  }

  onBeforeUnmount(() => {
    void connection.value?.stop()
  })

  return {
    connection,
    isConnected,
    isConnecting,
    connectionError,
    players,
    startConnection,
  }
}
