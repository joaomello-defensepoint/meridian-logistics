import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// SpaProxy launches this dev server on :3000; /api is proxied to the ASP.NET API.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5210',
    },
  },
})
