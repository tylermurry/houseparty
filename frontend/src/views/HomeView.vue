<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { createRoom as createRoomRequest } from '@/services/roomService'

const router = useRouter()
const isBusy = ref(false)
const isCreating = ref(false)
const status = ref('')
const logoUrl = new URL('../assets/images/HousePartyLogo.png', import.meta.url).href


async function createRoom() {
  status.value = ''
  isCreating.value = true
  let didNavigate = false
  try {
    isBusy.value = true
    const room = await createRoomRequest()
    if (!room.data?.id) {
      status.value = 'Failed to create room.'
      return
    }
    didNavigate = true
    await new Promise((resolve) => window.setTimeout(resolve, 300))
    router.push({ path: `/room/${room.data.id}`, state: { fromCreate: true } })
  } catch (error) {
    status.value = 'Failed to create room.'
    console.error(error)
  } finally {
    isBusy.value = false
    if (!didNavigate) {
      isCreating.value = false
    }
  }
}
</script>

<template>
  <main class="shell">
    <Transition name="home-logo">
      <div v-if="!isCreating" class="logo-wrap">
        <img class="logo" :src="logoUrl" alt="HouseParty logo" />
      </div>
    </Transition>
    <Transition name="home-button">
      <div v-if="!isCreating">
        <button class="create-room-button" :disabled="isBusy" @click="createRoom">CREATE ROOM</button>
      </div>
    </Transition>


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
  width: 400px;
  height: auto;
  animation: bob 2s ease-in-out infinite;
}

.create-room-button {
  margin-top: 3rem;
  width: 300px;
  height: 60px
}

.home-logo-leave-active {
  transition: opacity 260ms ease, transform 260ms ease;
}

.home-logo-leave-from {
  opacity: 1;
  transform: translateY(0);
}

.home-logo-leave-to {
  opacity: 0;
  transform: translateY(-100px);
}

.home-button-leave-active {
  transition: opacity 260ms ease, transform 260ms ease;
}

.home-button-leave-from {
  opacity: 1;
  transform: translateY(0);
}

.home-button-leave-to {
  opacity: 0;
  transform: translateY(100px);
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
