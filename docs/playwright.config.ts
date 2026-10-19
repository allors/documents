import { defineConfig } from '@playwright/test';
import { base } from './site.mjs';

const origin = 'http://127.0.0.1:4322';

export default defineConfig({
  testDir: './tests/browser',
  outputDir: './test-results',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  workers: process.env.CI ? 2 : undefined,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  reporter: 'list',
  use: {
    browserName: 'chromium',
    baseURL: `${origin}${base}`,
    colorScheme: 'light',
    reducedMotion: 'reduce',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1440, height: 1000 } } },
    { name: 'narrow', use: { viewport: { width: 390, height: 844 } } },
  ],
  webServer: {
    command: 'npm run preview -- --host 127.0.0.1 --port 4322 --ignore-lock',
    url: `${origin}${base}`,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
