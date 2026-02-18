import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
	testDir: './tests',
	workers: 1,
	reporter: [['html'], ['list']],
	use: {
		baseURL: 'http://localhost:5217',
		screenshot: 'only-on-failure',
	},
	webServer: {
		command: 'dotnet run --project TodoList.csproj --urls http://localhost:5217',
		url: 'http://localhost:5217',
		reuseExistingServer: true,
		timeout: 120_000,
	},
	projects: [
		{
			name: 'chromium',
			use: { ...devices['Desktop Chrome'] },
		},
	],
});
