# 0001. バックエンドへの OpenTelemetry 導入

## 状態

採用

## 背景

開発コンテナでは OpenTelemetry ダッシュボード（Aspire Dashboard）を起動し、`OTEL_EXPORTER_OTLP_ENDPOINT` も設定済みだった。しかしバックエンドに計装が無く、テレメトリは送られていなかった。
AGENTS.md には次の制約がある。

- 送信先は環境変数で指定し、設定ファイルに書かない。
- 環境変数が未設定、または `OTEL_SDK_DISABLED=true` の環境では登録しない。
- パスワードやトークンを含みうる値を記録しない。

## 検討した選択肢

- 送るシグナル
  - 案A: トレース＋ログ。compose.yaml のコメントどおりの範囲で、パッケージも少ない。
  - 案B: トレース＋ログ＋メトリクス。ダッシュボードで 3 種とも表示できる。追加パッケージはほぼ不要。
- 計装の範囲
  - 案A: ASP.NET Core＋HttpClient。DB アクセスの導入時に SQL 計装を追加する。
  - 案B: SqlClient も先に入れる。DB 導入時の追加作業が減るが、記録内容の設定に注意が必要。
- ランタイムメトリクス
  - 案A: `OpenTelemetry.Instrumentation.Runtime` パッケージを使う。
  - 案B: .NET 9 以降の組み込み Meter `System.Runtime` を `AddMeter` で購読する。依存パッケージが増えない。
- サービス名
  - 案A: SDK の既定に任せる。未指定だと `unknown_service:Backend` のような名前になる。
  - 案B: コードで既定値を持ち、`OTEL_SERVICE_NAME` があればそちらを優先する。

## 決定

- シグナルはトレース・ログ・メトリクスの 3 種とする。
- 計装は ASP.NET Core・HttpClient・SqlClient とする。SqlClient のパラメーター値は記録しない（実験的機能 `OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS` は有効にしない）。
- ランタイムメトリクスは組み込み Meter `System.Runtime` を使う。
- サービス名は既定で `try-tenants-backend` とし、`OTEL_SERVICE_NAME` があればそちらを使う。
- 送信先と有効・無効は環境変数（`OTEL_EXPORTER_OTLP_ENDPOINT`・`OTEL_SDK_DISABLED`）から直接読む。条件を満たさない場合は SDK 自体を登録しない。

## 理由

- Aspire Dashboard は 3 種のシグナルを表示でき、メトリクスを加えても追加の依存はほぼ無い。
- DB 導入前から SQL 計装を入れておけば、DB 導入の変更で計装の追加を忘れない。記録内容は既定（パラメーター値なし）に保つことで AGENTS.md の制約を満たす。
- 環境変数を IConfiguration 経由ではなく直接読むことで、appsettings に書いた値で送信が有効になる経路を作らない。
- `AddService` は環境変数から検出したサービス名を上書きする。そのため、`OTEL_SERVICE_NAME` が設定されている場合は呼ばない。

## 影響

- 開発コンテナ内で起動したバックエンドは、ダッシュボードへテレメトリを送る。
- テストプロジェクトを導入すると、開発コンテナの環境変数を引き継いでテレメトリを送る可能性がある。その時点で、テスト実行時に `OTEL_SDK_DISABLED=true` を設定するかを判断する。
- SqlClient 計装の動作は、DB アクセスの実装時に確認する。記録されるクエリ文にリテラルとして機微な値が埋め込まれないよう、クエリはパラメーター化する。
