import { describe, expect, it } from 'vitest'
import {
  MOUSE_GRID_SIZE,
  parseRawMousePresenceUpdate,
  quantizeMousePosition,
  toNormalizedMouseEvent,
} from './mousePresence'

describe('quantizeMousePosition', () => {
  it('returns null when viewport is invalid', () => {
    expect(quantizeMousePosition({ x: 10, y: 10 }, { width: 0, height: 100 })).toBeNull()
  })

  it('quantizes coordinates to grid bounds', () => {
    const quantized = quantizeMousePosition({ x: 50, y: 25 }, { width: 100, height: 50 })
    expect(quantized).toEqual({ x: 1024, y: 1024 })
  })

  it('clamps out-of-range pointer coordinates', () => {
    const quantized = quantizeMousePosition({ x: -10, y: 1000 }, { width: 100, height: 100 })
    expect(quantized).toEqual({ x: 0, y: MOUSE_GRID_SIZE - 1 })
  })
})

describe('parseRawMousePresenceUpdate', () => {
  it('parses camel-case payload', () => {
    const parsed = parseRawMousePresenceUpdate({
      playerNumber: 2,
      name: 'P2',
      x: 1200,
      y: 240,
    })

    expect(parsed).toEqual({
      playerNumber: 2,
      name: 'P2',
      x: 1200,
      y: 240,
    })
  })

  it('parses pascal-case payload with numeric strings', () => {
    const parsed = parseRawMousePresenceUpdate({
      PlayerNumber: '3',
      Name: 'P3',
      X: '2047',
      Y: '0',
    })

    expect(parsed).toEqual({
      playerNumber: 3,
      name: 'P3',
      x: 2047,
      y: 0,
    })
  })

  it('rejects invalid payload', () => {
    expect(parseRawMousePresenceUpdate({ playerNumber: 'bad', x: 1, y: 2 })).toBeNull()
  })
})

describe('toNormalizedMouseEvent', () => {
  it('converts grid coordinates to normalized range', () => {
    const normalized = toNormalizedMouseEvent({
      playerNumber: 4,
      name: 'P4',
      x: 2047,
      y: 1023.5,
    })

    expect(normalized.playerId).toBe('player-4')
    expect(normalized.normalizedX).toBe(1)
    expect(normalized.normalizedY).toBeCloseTo(0.5, 2)
  })
})
