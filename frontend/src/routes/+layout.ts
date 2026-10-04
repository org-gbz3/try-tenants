// 本番は ASP.NET Core が空の SPA シェル(index.html)を返すだけでサーバー描画が無いため、
// 開発サーバーでもサーバー描画を止め、ブラウザー API の利用可否などの挙動を本番と揃える。
export const ssr = false;
