export class Emitter<T> {
  private readonly listeners = new Set<(value: T) => void>()

  on(cb: (value: T) => void): () => void {
    this.listeners.add(cb)
    return () => {
      this.listeners.delete(cb)
    }
  }

  emit(value: T): void {
    for (const listener of this.listeners) {
      listener(value)
    }
  }

  clear(): void {
    this.listeners.clear()
  }
}
