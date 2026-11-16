import { defineConfig } from '@playwright/test'
export default defineConfig({
  testDir: './tests/e2e', testMatch: '**/studio*.spec.js', fullyParallel: false, workers: 1, timeout: 60000,
  use: { baseURL: process.env.PORTAL_TEST_URL || 'https://localhost:7173', ignoreHTTPSErrors: true, channel: 'chrome', headless: true, viewport: { width: 1440, height: 1050 } },
  reporter: [['list']], outputDir: 'test-results',
})
