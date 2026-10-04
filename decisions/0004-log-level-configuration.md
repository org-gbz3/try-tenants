# 0004. ログレベルの設定場所

## 状態

採用

## 背景

開発環境で EF Core が実行した SQL を構造化ログで確認したかった。しかし appsettings.json で `Microsoft.EntityFrameworkCore.Database.Command` が `Warning` に設定されていて、SQL のログが出力されていなかった。
修正にあたり、ログの設定を設定ファイルとコードに分散させず、一か所で管理したいという要望があった。

## 検討した選択肢

- 案A: コード(`builder.Logging.AddFilter`)に集約する。
  - 長所: 型があり、環境ごとの差をコードで表せる。
  - 短所: `AddFilter` は同じカテゴリーの設定ファイル・環境変数の値より優先されるため、運用中に環境変数でレベルを上げられず、変更に再配備が必要になる。`WebApplication.CreateBuilder` は `Logging` セクションを必ず読むので、設定ファイル側の経路は残り、完全には一か所にならない。.NET の一般的な慣習とも異なる。
- 案B: appsettings.json と appsettings.{環境名}.json に集約する。
  - 長所: 環境変数で再配備せずに上書きできる。.NET の慣習どおりで、環境ごとの差はファイルの上書きで表せる。appsettings の JSON はコメントを書けるため、設定の理由も残せる。
  - 短所: 設定値の誤りがビルドで検出されない。

## 決定

案B を採用する。ログレベルは appsettings.json と appsettings.{環境名}.json で定義し、各設定の意味と理由をコメントで記載する。コードではログレベルを指定しない。
OpenTelemetry のログ送信の有無や送信内容(`IncludeFormattedMessage` など)は、送信の構成に関わるためコード(`backend/Program.cs`)に置く。

## 理由

- 障害調査時に、再配備なしでログレベルを上げられることを重視した。
- コードに集約しても設定ファイルの経路は残るため、「一か所で管理する」という目的は設定ファイル側に寄せたほうが達成しやすい。

## 影響

- 開発環境では appsettings.Development.json で SQL のログを Information にして出力する。それ以外の環境では Warning 以上に絞る。
- 本番などで一時的にログを増やす場合は、`Logging__LogLevel__<カテゴリー>` の環境変数で上書きする。
