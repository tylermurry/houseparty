import { computed, ref } from 'vue'
import type { Component } from 'vue'

type GameModule = { default: Component }

type GameMeta = {
  title: string
  description: string
  thumbnail: string
}

type GameEntry = {
  id: string
  title: string
  description: string
  thumbnailUrl: string
  component: Component
}

const gameModules = import.meta.glob<GameModule>('@/games/*/Main.vue', { eager: true })
const metaModules = import.meta.glob<{ default: GameMeta }>('@/games/*/meta.json', { eager: true })
const thumbnailModules = import.meta.glob<string>('@/games/*/*.{png,jpg,jpeg,svg,webp}', {
  eager: true,
  query: '?url',
  import: 'default',
})

function resolveThumbnailUrl(gameId: string, fileName: string) {
  const match = Object.entries(thumbnailModules).find(([path]) =>
    path.endsWith(`/games/${gameId}/${fileName}`),
  )
  return match ? match[1] : ''
}

const gameEntries: GameEntry[] = Object.entries(metaModules)
  .map(([path, module]) => {
    const match = path.match(/\/games\/([^/]+)\/meta\.json$/)
    const id = match?.[1]
    if (!id) return null
    const component = gameModules[`/src/games/${id}/Main.vue`]?.default
    if (!component) return null
    const meta = module.default
    const thumbnailUrl = resolveThumbnailUrl(id, meta.thumbnail)
    if (!thumbnailUrl) return null
    return {
      id,
      title: meta.title,
      description: meta.description,
      thumbnailUrl,
      component,
    }
  })
  .filter((game): game is GameEntry => Boolean(game))
  .sort((a, b) => a.title.localeCompare(b.title))

export function useGameCatalog() {
  const selectedGameId = ref<string | null>(null)

  const activeGame = computed(() => {
    if (!selectedGameId.value) return null
    return gameEntries.find((game) => game.id === selectedGameId.value) ?? null
  })

  function selectGame(gameId: string) {
    selectedGameId.value = gameId
  }

  return {
    games: gameEntries,
    selectedGameId,
    activeGame,
    selectGame,
  }
}
