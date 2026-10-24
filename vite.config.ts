import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

const devPort = Number(process.env.MONEYBOARD_WEB_PORT ?? 5173)

export default defineConfig({ plugins: [react()], server: { host: 'localhost', port: devPort, strictPort: true, watch: { ignored: ['**/backend/**'] } }, test: { environment: 'jsdom', setupFiles: ['./src/testSetup.ts'], include: ['src/**/*.test.{ts,tsx}'] } })
