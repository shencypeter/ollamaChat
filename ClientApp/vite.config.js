import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
export default defineConfig(({ command }) => ({
  plugins: [vue()],
  server: { host: '127.0.0.1', port: 5173, strictPort: true, proxy: { '/api': { target: process.env.API_PROXY_TARGET || 'http://localhost:5084', changeOrigin: false } } },
  build: { outDir: '../wwwroot/portal', emptyOutDir: true },
  base: command === 'build' ? '/portal/' : '/',
  test: { environment: 'jsdom', include: ['src/**/*.test.js', 'tests/**/*.test.js'] }
}))
