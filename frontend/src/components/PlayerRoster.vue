<script setup lang="ts">
defineProps<{
  players: Array<{ number: number; name: string }>
}>()

const playerColor = (number: number) => {
  const normalized = ((number - 1) % 20) + 1
  return `var(--color-player-${normalized})`
}
</script>

<template>
  <div class="roster">
    <div class="roster-title">Players</div>
    <div v-if="players.length === 0" class="roster-empty">No one has joined yet.</div>
    <div v-for="player in players" :key="player.number" class="roster-row">
      <div class="roster-swatch" :style="{ backgroundColor: playerColor(player.number) }"></div>
      <div class="roster-name">{{ player.name }}</div>
    </div>
  </div>
</template>

<style scoped>
.roster {
  display: grid;
  gap: 10px;
  align-items: start;
}

.roster-title {
  font-size: 12px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #6c6c6c;
}

.roster-empty {
  font-size: 14px;
  color: #8b8b85;
}

.roster-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.roster-swatch {
  width: 24px;
  height: 24px;
  border: 3px solid #ffffff;
  box-sizing: border-box;
}

.roster-name {
  font-size: 16px;
  font-weight: 600;
}
</style>
