import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
  ],
  build: {
    rollupOptions: {
      onwarn(warning, warn) {

        // Exception for issue with @microsoft/signalr and Rollup that is out of our control
        if (warning?.loc?.file?.includes('@microsoft/signalr/dist/esm/Utils.js') && warning.message.includes('contains an annotation that Rollup cannot interpret')) return

        warn(warning)
      },
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
  },
})
