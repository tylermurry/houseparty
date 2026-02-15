import { describe, expect, it } from 'vitest'
import { Trace } from './trace'

describe('Trace', () => {
  it('Error logs errors only', () => {
    const entries: string[] = []
    const trace = new Trace({
      logLevel: 'error',
      logger: ({ severity, scope, message }) => entries.push(`${severity}:${scope}:${message}`),
    })

    trace.log('client', 'hello')
    trace.error('room', 'boom')

    expect(entries).toEqual(['error:room:boom'])
  })

  it('Info logs client and game scopes only', () => {
    const entries: string[] = []
    const trace = new Trace({
      logLevel: 'info',
      logger: ({ scope, message }) => entries.push(`${scope}:${message}`),
    })

    trace.log('client', 'init')
    trace.log('game', 'event')
    trace.log('room', 'update')
    trace.error('http', 'failed')

    expect(entries).toEqual(['client:init', 'game:event'])
  })

  it('Debug logs everything except HTTP', () => {
    const entries: string[] = []
    const trace = new Trace({
      logLevel: 'debug',
      logger: ({ scope, message }) => entries.push(`${scope}:${message}`),
    })

    trace.log('client', 'init')
    trace.log('realtime', 'connected')
    trace.log('room', 'joined')
    trace.log('http', 'request')
    trace.error('http', 'failed')

    expect(entries).toEqual(['client:init', 'realtime:connected', 'room:joined'])
  })

  it('Trace logs everything', () => {
    const entries: string[] = []
    const trace = new Trace({
      logLevel: 'trace',
      logger: ({ severity, scope, message }) => entries.push(`${severity}:${scope}:${message}`),
    })

    trace.log('http', 'request')
    trace.error('http', 'failed')

    expect(entries).toEqual(['info:http:request', 'error:http:failed'])
  })
})
