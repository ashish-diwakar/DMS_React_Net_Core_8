import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

export default defineConfig({
  plugins: [react()],
  css: {
    // Keep the parent Laravel PostCSS configuration out of this application.
    postcss: { plugins: [] },
  },
  server: {
    headers: { 'Referrer-Policy': 'no-referrer' },
    fs: {
      strict: true,
      allow: [fileURLToPath(new URL('.', import.meta.url))],
      deny: ['.env', '.env.*', '**/.local/**', '**/appsettings.Local.json', '**/*.{crt,pem}', '**/.git/**'],
    },
    host: '127.0.0.1',
    port: 5178,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5088',
      },
    },
  },
  preview: {
    host: '127.0.0.1',
    port: 4178,
    strictPort: true,
  },
});
