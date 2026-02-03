import API from '@/api-client/client'

export type SignalrNegotiation = { url: string; accessToken: string }

export async function negotiateConnection(): Promise<SignalrNegotiation> {
  const response = await API.postApiSignalrNegotiate()
  if (!response.data?.url || !response.data?.accessToken) {
    throw new Error('Failed to negotiate SignalR connection.')
  }
  return { url: response.data.url, accessToken: response.data.accessToken }
}
