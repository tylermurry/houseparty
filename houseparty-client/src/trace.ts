export type TraceType = 'client' | 'http' | 'realtime' | 'room' | 'game'
export type LogLevel = 'error' | 'info' | 'debug' | 'trace'
export type LogSeverity = 'error' | 'info'

export type TraceLogger = (entry: {
  level: LogLevel
  severity: LogSeverity
  scope: TraceType
  message: string
  data?: unknown
}) => void

export class Trace {
  private readonly level: LogLevel
  private readonly logger: TraceLogger

  constructor(options?: { logLevel?: LogLevel; logger?: TraceLogger }) {
    this.level = options?.logLevel ?? 'error'
    this.logger = options?.logger ?? defaultTraceLogger
  }

  log(scope: TraceType, message: string, data?: unknown): void {
    this.info(scope, message, data)
  }

  traceOnly(scope: TraceType, message: string, data?: unknown): void {
    if (this.level !== 'trace') {
      return
    }

    this.write('info', scope, message, data)
  }

  info(scope: TraceType, message: string, data?: unknown): void {
    this.write('info', scope, message, data)
  }

  error(scope: TraceType, message: string, data?: unknown): void {
    this.write('error', scope, message, data)
  }

  private write(severity: LogSeverity, scope: TraceType, message: string, data?: unknown): void {
    if (!this.shouldLog(severity, scope)) {
      return
    }

    this.logger({
      level: this.level,
      severity,
      scope,
      message,
      data,
    })
  }

  private shouldLog(severity: LogSeverity, scope: TraceType): boolean {
    if (this.level === 'trace') {
      return true
    }

    if (this.level === 'debug') {
      return scope !== 'http'
    }

    if (this.level === 'info') {
      return scope === 'client' || scope === 'game'
    }

    return severity === 'error'
  }
}

const defaultTraceLogger: TraceLogger = ({ severity, scope, message, data }) => {
  const prefix = `[houseparty-client:${scope}]`

  if (severity === 'error') {
    if (data === undefined) {
      console.error(`${prefix} ${message}`)
      return
    }

    console.error(`${prefix} ${message}`, data)
    return
  }

  if (data === undefined) {
    console.debug(`${prefix} ${message}`)
    return
  }

  console.debug(`${prefix} ${message}`, data)
}
