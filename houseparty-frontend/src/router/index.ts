import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import RoomView from '../views/RoomView.vue'
import KitchenSink from "../views/KitchenSink.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', component: HomeView },
    { path: '/room/:roomId', component: RoomView },
    { path: '/kitchen-sink', component: KitchenSink }
  ],
})

export default router
