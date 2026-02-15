import { describe, expect, it } from 'vitest'
import { HousePartyError } from '../errors'
import { parseGameEvent, projectObjectLocks } from './gameEvents'

describe('parseGameEvent', () => {
  it('parses ControlObjectEvent', () => {
    const parsed = parseGameEvent({
      sequence: 1,
      name: 'ControlObjectEvent',
      playerId: 'player-1',
      timestamp: 123,
      objectId: 'turn',
    })

    expect(parsed.name).toBe('ControlObjectEvent')
    expect(parsed.objectId).toBe('turn')
  })

  it('throws for unknown event name', () => {
    expect(() =>
      parseGameEvent({
        sequence: 1,
        name: 'UnknownEvent',
        playerId: 'player-1',
        timestamp: 123,
      }),
    ).toThrow(HousePartyError)
  })

  it('parses event name and fields case-insensitively', () => {
    const parsed = parseGameEvent({
      Sequence: 1,
      Name: 'controlobjectevent',
      PlayerId: 'player-1',
      Timestamp: 123,
      ObjectId: 'turn',
    })

    expect(parsed.name).toBe('ControlObjectEvent')
    expect(parsed.objectId).toBe('turn')
    expect(parsed.playerId).toBe('player-1')
  })
})

describe('projectObjectLocks', () => {
  it('handles control->release', () => {
    const locks = projectObjectLocks([
      {
        sequence: 1,
        name: 'ControlObjectEvent',
        playerId: 'player-1',
        timestamp: 1,
        objectId: 'turn',
      },
      {
        sequence: 2,
        name: 'ReleaseObjectEvent',
        playerId: 'player-1',
        timestamp: 2,
        objectId: 'turn',
      },
    ])

    expect(locks).toEqual([{ objectId: 'turn', holderPlayerId: null }])
  })

  it('handles repeated control by different players', () => {
    const locks = projectObjectLocks([
      {
        sequence: 1,
        name: 'ControlObjectEvent',
        playerId: 'player-1',
        timestamp: 1,
        objectId: 'turn',
      },
      {
        sequence: 2,
        name: 'ControlObjectEvent',
        playerId: 'player-2',
        timestamp: 2,
        objectId: 'turn',
      },
    ])

    expect(locks).toEqual([{ objectId: 'turn', holderPlayerId: 'player-2' }])
  })
})
