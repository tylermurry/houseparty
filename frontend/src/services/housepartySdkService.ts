import { createHousePartyClient } from '@houseparty/sdk'

const baseUrl = import.meta.env.VITE_BACKEND_API_URL ?? ''

export const housepartyClient = createHousePartyClient({
  baseUrl,
})
