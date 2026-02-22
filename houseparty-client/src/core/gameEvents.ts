import { HousePartyError } from '../errors'
import type {
  EventSchema,
  GameEvent,
  GameObjectLock,
} from '../generated/events'
import { GAME_EVENT_SCHEMAS } from '../generated/events'

const EVENT_SCHEMA_BY_NAME = new Map<string, EventSchema>(
  GAME_EVENT_SCHEMAS.map((schema) => [schema.name.toLowerCase(), schema]),
)

function requireBaseFields(raw: Record<string, unknown>): {
  sequence: number
  name: string
  playerId: string
  timestamp: number
} {
  const sequence = getField(raw, 'sequence')
  const name = getField(raw, 'name')
  const playerId = getField(raw, 'playerId')
  const timestamp = getField(raw, 'timestamp')

  if (typeof sequence !== 'number' || typeof name !== 'string' || typeof playerId !== 'string' || typeof timestamp !== 'number') {
    throw new HousePartyError('PROJECTION_ERROR', 'Invalid game event base fields.')
  }

  return { sequence, name, playerId, timestamp }
}

function requireStringField(raw: Record<string, unknown>, key: string): string {
  const value = getField(raw, key)
  if (typeof value !== 'string') {
    throw new HousePartyError('PROJECTION_ERROR', `Invalid game event field '${key}'.`)
  }

  return value
}

function getField(raw: Record<string, unknown>, key: string): unknown {
  if (key in raw) {
    return raw[key]
  }

  const target = key.toLowerCase()
  for (const [candidateKey, value] of Object.entries(raw)) {
    if (candidateKey.toLowerCase() === target) {
      return value
    }
  }

  return undefined
}

function normalizeEventName(name: string): GameEvent['name'] | null {
  return EVENT_SCHEMA_BY_NAME.get(name.toLowerCase())?.name ?? null
}

function isExpectedType(value: unknown, expectedType: 'string' | 'number' | 'boolean' | 'unknown'): boolean {
  if (expectedType === 'unknown') {
    return value !== undefined
  }

  return typeof value === expectedType
}

export function parseGameEvent(rawEvent: unknown): GameEvent {
  if (!rawEvent || typeof rawEvent !== 'object') {
    throw new HousePartyError('PROJECTION_ERROR', 'Game event payload is not an object.')
  }

  const raw = rawEvent as Record<string, unknown>
  const base = requireBaseFields(raw)
  const normalizedName = normalizeEventName(base.name)

  if (!normalizedName) {
    throw new HousePartyError('PROJECTION_ERROR', `Unknown game event '${base.name}'.`)
  }

  const schema = EVENT_SCHEMA_BY_NAME.get(normalizedName.toLowerCase())
  if (!schema) {
    throw new HousePartyError('PROJECTION_ERROR', `Missing schema for game event '${normalizedName}'.`)
  }

  const eventPayload: Record<string, unknown> = {
    ...base,
    name: schema.name,
  }

  for (const field of schema.fields) {
    const value = getField(raw, field.name)

    if (!isExpectedType(value, field.type)) {
      throw new HousePartyError('PROJECTION_ERROR', `Invalid game event field '${field.name}'.`)
    }

    eventPayload[field.name] = value
  }

  const typeDiscriminator = getField(raw, '$type')
  if (typeof typeDiscriminator === 'string') {
    eventPayload.$type = typeDiscriminator
  }

  return eventPayload as GameEvent
}

export function projectObjectLocks(events: readonly GameEvent[]): readonly GameObjectLock[] {
  const map = new Map<string, string | null>()

  for (const event of events) {
    const schema = EVENT_SCHEMA_BY_NAME.get(event.name.toLowerCase())
    if (!schema || schema.objectLockEffect === 'none') {
      continue
    }

    const objectId = requireStringField(event as unknown as Record<string, unknown>, 'objectId')

    if (schema.objectLockEffect === 'acquire') {
      map.set(objectId, event.playerId)
      continue
    }

    map.set(objectId, null)
  }

  return Array.from(map.entries()).map(([objectId, holderPlayerId]) => ({ objectId, holderPlayerId }))
}
