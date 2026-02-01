<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import API from '@/api-client/client';

const router = useRouter()
const isBusy = ref(false)
const status = ref('')
const logoUrl = new URL('../assets/images/HousePartyLogo.png', import.meta.url).href


async function createRoom() {
  status.value = ''
  try {
    isBusy.value = true
    const room = await API.postApiRooms()
    if (!room.data?.id) {
      status.value = 'Failed to create room.'
      return
    }
    await router.push(`/room/${room.data.id}`)
  } catch (error) {
    status.value = 'Failed to create room.'
    console.error(error)
  } finally {
    isBusy.value = false
  }
}
</script>

<template>
  <main class="shell">
    <div class="logo-wrap">
      <img class="logo" :src="logoUrl" alt="HouseParty logo" />
    </div>
    <div>
      <button class="create-room-button" :onclick="createRoom">CREATE ROOM</button>
    </div>


  </main>
</template>

<style scoped>
.shell {
  min-height: 100vh;
  width: 100%;
  padding: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.logo-wrap {
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100%;
  height: 100%;
}

.logo {
  width: 20vw;
  height: auto;
  animation: bob 2s ease-in-out infinite;
}

.create-room-button {
  margin-top: 3rem;
  width: 300px;
  height: 60px
}

@keyframes bob {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-15px);
  }
}

@media (prefers-reduced-motion: reduce) {
  .logo {
    animation: none;
  }
}

</style>
