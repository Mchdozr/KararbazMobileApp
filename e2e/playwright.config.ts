import { defineConfig, devices } from '@playwright/test';

// Uçtan uca testler web derlemesi üzerinde koşar. API sahte mekan verisiyle ve bellek içi veritabanıyla
// başlar (Postgres/Redis gerekmez). Web'de harita yer tutucudur; diğer her ekran gerçek API ile çalışır.
const API_PORT = Number(process.env.E2E_API_PORT ?? 5090);
const WEB_PORT = Number(process.env.E2E_WEB_PORT ?? 8090);
export const API = `http://localhost:${API_PORT}`;

export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  workers: 2,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    ...devices['Pixel 7'],
    locale: 'tr-TR',
    timezoneId: 'Europe/Istanbul',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --project ../src/Nereye.Api --no-launch-profile',
      url: `${API}/health/live`,
      timeout: 240_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: API,
        Database__UseInMemory: 'true',
        Database__Name: 'nereye-e2e',
        Redis__UseMemory: 'true',
        Places__UseFake: 'true',
      },
    },
    {
      // E2E_SKIP_EXPORT=1: .web zaten güncelse derlemeyi atla (yerelde tekrar çalıştırırken hızlı).
      command:
        `if [ "$E2E_SKIP_EXPORT" != "1" ] || [ ! -f .web/index.html ]; then ` +
        `(cd ../apps/nereye-go && EXPO_PUBLIC_API_URL=${API} npx expo export -p web --output-dir ../../e2e/.web); fi && ` +
        `npx serve -s -l ${WEB_PORT} .web`,
      url: `http://localhost:${WEB_PORT}`,
      timeout: 420_000,
      reuseExistingServer: !process.env.CI,
    },
  ],
});
