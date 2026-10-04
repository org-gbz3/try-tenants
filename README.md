# try-tenants

Docker Compose で mise 入りの開発コンテナと SQL Server を起動します。

## 構成

- 開発環境: `debian:bookworm-slim` に Git、curl、SSH クライアント、sudo、展開用ユーティリティと mise を追加。非 root の `vscode` ユーザーで作業します。
- mise: Dockerfile の `MISE_VERSION` で固定。開発ツールは `mise.toml` で管理し、イメージのビルド時にインストールします。Bash の有効化と shims の PATH 設定により、ターミナルとエディターから利用できます。
- SQL Server: 2025 Developer Edition。クエリが成功してから開発コンテナを起動します。データは名前付きボリュームに保存します。
- OpenTelemetry ダッシュボード: Aspire Dashboard。バックエンドのトレース・ログ・メトリクスを表示します。保持はメモリのみで、再起動すると消えます。詳細は [OpenTelemetry](#opentelemetry) を参照。
- `backend/` — ASP.NET Core Web API（Controllers ベース、.NET 10）。`wwwroot` に配置された静的ファイルを配信し、API は `/api` 配下。
- `frontend/` — SvelteKit（`@sveltejs/adapter-static` によるSPAビルド）。ビルド出力は直接 `backend/wwwroot` へ書き出される。SPA 専用のため SSR は無効（`src/routes/+layout.ts`）で、サーバーで実行されるファイル（`*.server.ts`・`+server.ts`・`src/lib/server/`）を置くとビルドが失敗する。
- `docs/` — 機能ドメイン別のER図と、画面操作とCRUD操作の対応表。現時点の仕様を示す資料で、対応するエンティティ・API・画面を変更するときに更新する。詳細は [docs/README.md](docs/README.md) を参照。
- `decisions/` — 方針・仕様を検討した経緯（ADR）。現時点の仕様そのものは README.md, `docs/` 側に記載し、`decisions/` にはなぜその決定に至ったかを記録する。詳細は [decisions/README.md](decisions/README.md) を参照。

## 起動

Docker Engine / Docker Desktop、Docker Compose、VS Code の Dev Containers 拡張機能が必要です。
SQL Server の公式対応環境は Linux x86-64 です。SQL Server 用に最低 2 GB、開発環境用には追加のメモリを確保してください。ARM ホストでのエミュレーションは公式サポート対象外です。

1. リポジトリのルートで `cp .devcontainer/.env.example .devcontainer/.env` を実行します。
2. `.devcontainer/.env` の `MSSQL_SA_PASSWORD` を変更します。8 文字以上で、英大文字・英小文字・数字・記号のうち 3 種類以上を含めてください。`.env` は Git 管理対象外です。
3. VS Code で **Dev Containers: Reopen in Container** を実行します。

Compose の `ACCEPT_EULA=Y` は SQL Server のライセンス条項への同意を表します。Developer Edition は開発・テスト用途で使用します。

## 開発ツール

フロントエンドは `frontend/` に SvelteKit、バックエンドは `backend/` に ASP.NET Core を配置します。
mise で Node.js 24 系（npm 同梱）と .NET SDK 10.0 系を導入します。SvelteKit 自体はフロントエンドの npm 依存関係として管理します。
現在の指定は完全なバージョンで固定しています。再現性を維持するため、更新時は `mise.toml` を明示的に変更してコミットしてください。

コンテナ内で `mise use <tool>@<version>` を実行してツールを追加し、更新された `mise.toml` をコミットします。
設定変更を反映するときは `mise install` を実行してください。
ツール固有の OS ライブラリやコンパイラが必要な場合は Dockerfile に追加してください。
.NET の実行に必要な Debian ライブラリは Dockerfile に追加済みです。

## アプリケーションの初期作成

devcontainer 環境起動後、初期作成時に使用するコマンドです。

```sh
npx sv create frontend
npm --prefix frontend install
dotnet new webapi --name Backend --output backend --framework net10.0 --no-https
```

フロントエンドのパッケージ管理には npm を使用し、生成された `package-lock.json` をコミットします。
以後の依存関係の復元には `npm --prefix frontend ci` と `dotnet restore backend` を使用します。

## SQL Server への接続

開発コンテナからの接続先は `sqlserver,1433`、ユーザーは `sa`、パスワードは `.devcontainer/.env` の値です。
開発用の自己署名証明書を使用するため、クライアントで必要に応じて `TrustServerCertificate=True` を設定します。

ホストから付属の sqlcmd を使って接続確認できます。

```sh
docker compose -f .devcontainer/compose.yaml exec sqlserver bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT @@VERSION"'
```

停止は `docker compose -f .devcontainer/compose.yaml down` で行えます。データは保持されます。
`down -v` はデータも削除するため、初期化したい場合にのみ使用してください。
既存データがある場合、`.env` の変更だけでは `sa` のパスワードは変更されません。

`2025-latest` は更新されるタグです。SQL Server の厳密な再現性が必要な場合は検証済みの CU タグまたは digest に固定してください。

## OpenTelemetry

バックエンドは OTLP でトレース・ログ・メトリクスを送信します。ダッシュボードの画面はホストの `http://localhost:18888` で開けます（ポートは `.devcontainer/.env` の `OTEL_DASHBOARD_PORT` で変更）。

- 送信先は環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` で指定します。開発コンテナでは compose.yaml で `http://otel-dashboard:18889`（gRPC）を設定済みです。
- `OTEL_EXPORTER_OTLP_ENDPOINT` が未設定、または `OTEL_SDK_DISABLED=true` の場合は OpenTelemetry を登録せず、何も送信しません。
- サービス名は既定で `try-tenants-backend` です。`OTEL_SERVICE_NAME` を設定するとその値を使います。
- 計装の範囲: ASP.NET Core（受信リクエスト）、HttpClient（送信リクエスト）、SqlClient（SQL Server へのクエリ）、ランタイムメトリクス（`System.Runtime`）。
- リクエスト本文や SQL パラメーター値は記録しません。URL のクエリ文字列はマスクされます。
- メトリクスは既定で 60 秒ごとに送信されます。すぐに確認したい場合は `OTEL_METRIC_EXPORT_INTERVAL`（ミリ秒）を短くしてください。

## よく使うコマンド

```bash
# 確認
npm --prefix frontend run check
npm --prefix frontend run test
dotnet build backend
dotnet test backend.Tests
dotnet publish backend -c Release

# マイグレーション適用
dotnet ef database update --project backend

# 開発サーバ起動用ワンライナー  see: http://localhost:5000/
mise run dev
```
