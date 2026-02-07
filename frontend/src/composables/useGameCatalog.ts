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
  isMock?: boolean
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

const realGameEntries: GameEntry[] = Object.entries(metaModules)
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

const fallbackComponent = realGameEntries[0]?.component
const fallbackThumbnailUrl = realGameEntries[0]?.thumbnailUrl ?? ''
const mockEntries: GameEntry[] = fallbackComponent
  ? [
      {
        id: 'mock-arcade-blitz',
        title: 'Arcade Blitz',
        description: 'Rapid-fire minigames with escalating chaos.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-cosmic-poker',
        title: 'Cosmic Poker',
        description: 'Galactic stakes and wild starfield draws.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-mystic-tiles',
        title: 'Mystic Tiles',
        description: 'Flip, match, and outsmart the deck.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-neon-roulette',
        title: 'Neon Roulette',
        description: 'Spin for luck with a synthwave twist.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-rocket-rally',
        title: 'Rocket Rally',
        description: 'Launch and land with perfect timing.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-shadow-sprint',
        title: 'Shadow Sprint',
        description: 'Outrun the lights before they catch you.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-skyline-sabotage',
        title: 'Skyline Sabotage',
        description: 'Team up to disable the tower defenses.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-stellar-strings',
        title: 'Stellar Strings',
        description: 'Pluck the right chords to win the round.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-turbo-trivia',
        title: 'Turbo Trivia',
        description: 'Answer fast, steal points faster.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
      {
        id: 'mock-vault-heist',
        title: 'Vault Heist',
        description: 'Crack the code before the timer hits zero.',
        thumbnailUrl: fallbackThumbnailUrl,
        component: fallbackComponent,
        isMock: true,
      },
    ]
  : []

const gameEntries: GameEntry[] = [...realGameEntries, ...mockEntries]

export function useGameCatalog() {
  const selectedGameId = ref<string | null>(null)

  const activeGame = computed(() => {
    if (!selectedGameId.value) return null
    return gameEntries.find((game) => game.id === selectedGameId.value) ?? null
  })

  function selectGame(gameId: string) {
    const entry = gameEntries.find((game) => game.id === gameId)
    if (!entry || entry.isMock) return
    selectedGameId.value = gameId
  }

  return {
    games: gameEntries,
    selectedGameId,
    activeGame,
    selectGame,
  }
}
