<script setup lang="ts">
const playerColors = [
  { name: "Player 1", color: "var(--color-player-1)" },
  { name: "Player 2", color: "var(--color-player-2)" },
  { name: "Player 3", color: "var(--color-player-3)" },
  { name: "Player 4", color: "var(--color-player-4)" },
  { name: "Player 5", color: "var(--color-player-5)" },
  { name: "Player 6", color: "var(--color-player-6)" },
  { name: "Player 7", color: "var(--color-player-7)" },
  { name: "Player 8", color: "var(--color-player-8)" },
  { name: "Player 9", color: "var(--color-player-9)" },
  { name: "Player 10", color: "var(--color-player-10)" },
  { name: "Player 11", color: "var(--color-player-11)" },
  { name: "Player 12", color: "var(--color-player-12)" },
  { name: "Player 13", color: "var(--color-player-13)" },
  { name: "Player 14", color: "var(--color-player-14)" },
  { name: "Player 15", color: "var(--color-player-15)" },
  { name: "Player 16", color: "var(--color-player-16)" },
  { name: "Player 17", color: "var(--color-player-17)" },
  { name: "Player 18", color: "var(--color-player-18)" },
  { name: "Player 19", color: "var(--color-player-19)" },
  { name: "Player 20", color: "var(--color-player-20)" },
]
</script>
<template>
  <div style="margin:50px">
    <H1>BUTTONS</H1>
    <button style="background-color: var(--color-primary);">PLAY GAME</button>

    <H1 style="margin-top: 50px">PLAYER COLORS</H1>
    <div class="player-color-list">
      <div v-for="player in playerColors" :key="player.name" class="player-color-item">
        <div class="player-color-swatch" :style="{ backgroundColor: player.color }"></div>
        <div class="player-color-name">{{ player.name }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.player-color-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px 24px;
  margin-top: 12px;
}

.player-color-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.player-color-swatch {
  width: 28px;
  height: 28px;
  border: 4px solid #ffffff;
  box-sizing: border-box;
}

.player-color-name {
  font-weight: 600;
}
</style>
