import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { globSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { Plugin } from 'vite';

// SPA として ASP.NET Core から静的配信するため、サーバーで実行されるファイルは本番では動かない。
// adapter-static は fallback 指定時に動的ルートを検査せずビルドが通ってしまうため、ここで検出して止める。
const serverOnlyPatterns = [
	'src/**/*.server.{js,ts}',
	'src/routes/**/+server.{js,ts}',
	'src/lib/server/**/*'
];

function forbidServerFiles(): Plugin {
	return {
		name: 'forbid-server-files',
		buildStart() {
			const root = fileURLToPath(new URL('.', import.meta.url));
			const found = globSync(serverOnlyPatterns, { cwd: root });
			if (found.length > 0) {
				this.error(
					`frontend は SPA 専用のため、サーバーで実行されるファイルは置けません。データは /api から取得してください:\n${found.map((file) => `  - ${file}`).join('\n')}`
				);
			}
		}
	};
}

export default defineConfig({
	plugins: [
		forbidServerFiles(),
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			adapter: adapter({
				pages: '../backend/wwwroot',
				assets: '../backend/wwwroot',
				fallback: 'index.html'
			})
		})
	],
	test: {
		expect: { requireAssertions: true },
		projects: [
			{
				extends: './vite.config.ts',
				test: {
					name: 'client',
					browser: {
						enabled: true,
						provider: playwright(),
						instances: [{ browser: 'chromium', headless: true }]
					},
					include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
					exclude: ['src/lib/server/**']
				}
			},

			{
				extends: './vite.config.ts',
				test: {
					name: 'server',
					environment: 'node',
					include: ['src/**/*.{test,spec}.{js,ts}'],
					exclude: ['src/**/*.svelte.{test,spec}.{js,ts}']
				}
			}
		]
	}
});
