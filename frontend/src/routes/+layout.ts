import { loadMe } from '#lib/auth.svelte.ts';

// 本番は ASP.NET Core が空の SPA シェル(index.html)を返すだけでサーバー描画が無いため、
// 開発サーバーでもサーバー描画を止め、ブラウザー API の利用可否などの挙動を本番と揃える。
export const ssr = false;

// 画面ごとの表示可否を判断できるよう、最初の描画前にサインイン状態を確定させる。
export async function load() {
	try {
		await loadMe();
	} catch {
		// API に到達できなくても画面自体は表示し、各操作のエラー表示に任せる。
	}
}
