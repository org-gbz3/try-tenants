# try-tenants

Docker Compose で mise 入りの開発コンテナと SQL Server を起動します。

## 構成

- 開発環境: `debian:bookworm-slim` に Git、curl、SSH クライアント、sudo、展開用ユーティリティと mise を追加。非 root の `vscode` ユーザーで作業します。
- mise: Dockerfile の `MISE_VERSION` で固定。開発ツールは `mise.toml` で管理し、イメージのビルド時にインストールします。Bash の有効化と shims の PATH 設定により、ターミナルとエディターから利用できます。
- SQL Server: 2025 Developer Edition。クエリが成功してから開発コンテナを起動します。データは名前付きボリュームに保存します。

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

## よく使うコマンド

```bash
# ワンライナー  see: http://localhost:5000/
npm --prefix frontend run build && dotnet run --project backend --no-launch-profile --urls http://0.0.0.0:5000
```
