import { describe, expect, it } from 'vitest'
import { Emitter } from './emitter'

describe('Emitter', () => {
  it('emits values to subscribers and supports unsubscribe', () => {
    const emitter = new Emitter<number>()
    const values: number[] = []

    const unsubscribe = emitter.on((value) => {
      values.push(value)
    })

    emitter.emit(1)
    unsubscribe()
    emitter.emit(2)

    expect(values).toEqual([1])
  })
})
