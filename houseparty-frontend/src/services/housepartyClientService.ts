import { createHousePartyClient } from '@houseparty/client'

const baseUrl = import.meta.env.VITE_BACKEND_API_URL ?? ''

export const housepartyClient = createHousePartyClient({
  baseUrl,
})
