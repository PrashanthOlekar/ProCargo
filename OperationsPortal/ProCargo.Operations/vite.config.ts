/// <reference types="vitest/config" />
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// The API is reached through the dev-server proxy so the refresh-token cookie stays same-origin
// (HttpOnly, SameSite=Strict, path /api/v1/auth). In Azure the same is achieved with a /api route
// on the Static Web App / Front Door, or by hosting the API on a sub-domain of the same site.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const apiTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:7180';

  return {
    plugins: [react()],
    server: {
      port: 5174,
      strictPort: true,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true, secure: false },
      },
    },
    preview: { port: 4174 },
    build: {
      sourcemap: false,
      chunkSizeWarningLimit: 900,
      rollupOptions: {
        output: {
          manualChunks: {
            react: ['react', 'react-dom', 'react-router-dom'],
            mui: ['@mui/material', '@emotion/react', '@emotion/styled'],
            data: ['@tanstack/react-query', 'axios', 'zod', 'react-hook-form'],
          },
        },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
    },
  };
});
