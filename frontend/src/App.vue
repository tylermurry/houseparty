<script setup lang="ts">
import { ref } from 'vue'
import { AnonymousAuthenticationProvider } from '@microsoft/kiota-abstractions'
import { FetchRequestAdapter } from '@microsoft/kiota-http-fetchlibrary'
import { createHousePartyServerClient } from './api-client/housePartyServerClient'

type Person = {
  id: string
  firstName: string
  lastName: string
}

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5407'
const adapter = new FetchRequestAdapter(new AnonymousAuthenticationProvider())
adapter.baseUrl = baseUrl
const client = createHousePartyServerClient(adapter)

const firstName = ref('')
const lastName = ref('')
const lookupId = ref('')
const person = ref<Person | null>(null)
const status = ref('')
const isBusy = ref(false)
const decoder = new TextDecoder()

function parsePerson(buffer?: ArrayBuffer): Person | null {
  if (!buffer) return null
  const text = decoder.decode(new Uint8Array(buffer))
  if (!text) return null
  return JSON.parse(text) as Person
}

async function createPerson() {
  status.value = ''
  person.value = null
  const trimmedFirst = firstName.value.trim()
  const trimmedLast = lastName.value.trim()
  if (!trimmedFirst || !trimmedLast) {
    status.value = 'First and last name are required.'
    return
  }

  try {
    isBusy.value = true
    const response = await client.api.person.post({
      queryParameters: {
        firstName: trimmedFirst,
        lastName: trimmedLast
      }
    })
    const created = parsePerson(response)
    if (!created) {
      status.value = 'No response body received.'
      return
    }
    person.value = created
    lookupId.value = created.id
    status.value = 'Created.'
  } catch (error) {
    status.value = 'Create failed.'
    console.error(error)
  } finally {
    isBusy.value = false
  }
}

async function getPerson() {
  status.value = ''
  person.value = null
  const trimmedId = lookupId.value.trim()
  if (!trimmedId) {
    status.value = 'Person id is required.'
    return
  }

  try {
    isBusy.value = true
    const response = await client.api.person.get({
      queryParameters: {
        id: trimmedId
      }
    })
    const fetched = parsePerson(response)
    if (!fetched) {
      status.value = 'Not found.'
      return
    }
    person.value = fetched
    status.value = 'Loaded.'
  } catch (error) {
    status.value = 'Lookup failed.'
    console.error(error)
  } finally {
    isBusy.value = false
  }
}
</script>

<template>
  <main class="shell">
    <header>
      <h1>HouseParty</h1>
      <p class="hint">API: {{ baseUrl }}</p>
    </header>

    <section class="panel">
      <h2>Create person</h2>
      <div class="row">
        <input v-model="firstName" placeholder="First name" />
        <input v-model="lastName" placeholder="Last name" />
      </div>
      <button :disabled="isBusy" @click="createPerson">Create</button>
    </section>

    <section class="panel">
      <h2>Get person</h2>
      <div class="row">
        <input v-model="lookupId" placeholder="Person id" />
      </div>
      <button :disabled="isBusy" @click="getPerson">Get</button>
    </section>

    <section class="panel">
      <h2>Result</h2>
      <pre v-if="person">{{ JSON.stringify(person, null, 2) }}</pre>
      <p v-else class="hint">No data loaded.</p>
      <p class="status" v-if="status">{{ status }}</p>
    </section>
  </main>
</template>

<style scoped>
:global(body) {
  margin: 0;
  font-family: 'Courier New', Courier, monospace;
  background: #f6f6f2;
  color: #1c1c1c;
}

.shell {
  max-width: 720px;
  margin: 48px auto;
  padding: 0 20px 60px;
  display: grid;
  gap: 24px;
}

header h1 {
  margin: 0 0 8px;
  font-size: 28px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.hint {
  margin: 0;
  color: #6c6c6c;
  font-size: 14px;
}

.panel {
  border: 1px solid #d0d0c8;
  padding: 16px;
  background: #ffffff;
}

.panel h2 {
  margin: 0 0 12px;
  font-size: 16px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.row {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  margin-bottom: 12px;
}

input {
  font-family: inherit;
  font-size: 14px;
  padding: 8px;
  border: 1px solid #bdbdb4;
  background: #fafaf8;
}

button {
  font-family: inherit;
  padding: 8px 14px;
  border: 1px solid #1c1c1c;
  background: #1c1c1c;
  color: #f6f6f2;
  cursor: pointer;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

pre {
  background: #f2f2ed;
  padding: 12px;
  margin: 0;
  white-space: pre-wrap;
}

.status {
  margin-top: 12px;
  font-size: 14px;
}
</style>
