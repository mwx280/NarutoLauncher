import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 构建产物输出到 app/ui/dist（CEF 宿主加载该目录的 index.html）
// base 用相对路径，保证 file:// 或本地 HTTP 加载都能解析资源
export default defineConfig({
  plugins: [vue()],
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true
  },
  server: {
    port: 5173,
    host: true
  }
})
