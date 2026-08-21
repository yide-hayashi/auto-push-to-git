# AI SQL最適化 操作・障害対応

利用者向けの画面付き手順は [AI SQL最適化 操作説明書（日台版）](AI_SQL最適化_操作説明書_日台版.docx) を参照する。

## 目的

この画面は、ユーザーが承認したOracleの単一SELECTについて、AI Agentが実行計画を比較し、選んだ最終候補だけを実測するためのものです。DDL、DML、PL/SQL、索引作成、統計更新、DB設定変更は実行しません。変更案はAI分析欄へ「未実行の提案」として記録するだけです。

SQL本文とSQLコメントはAgentへ送信され、セッション履歴へ保存される。個人情報、パスワード、Token、業務上の秘密値をリテラルやコメントへ書かず、承認前にバインド変数へ置き換える。バインド値、結果行、Oracle認証情報、Bearer TokenはAgentへ送信・保存しない。

## 通常の操作

1. メイン画面または通知領域から「AI SQL最適化」を開く。
2. `KTGH00`、`HPK210`、`TSHIS` のいずれかを選ぶ。
3. 単一の `SELECT` または `WITH ... SELECT` を入力する。
4. SQL中の全バインドについて、名前、Oracle型、NULL、値、必要ならサイズを入力する。
5. 目的と制限を確認する。既定値は最終実行60秒、画面200行（設定範囲1～1000行）、1ラウンド10候補。画面行数は結果プレビューだけの上限で、選択した最終候補は実測計画のため1回だけ全件を読取る。
6. 「初回承認」を押す。承認後にSQL、接続先、バインド表、最適化目標、制限を変更すると承認は失効する。
7. 枠付きの「Codex実行設定」でモデル、推論量、速度を確認してから、アプリ内Agentなら「Codexで開始」を押す。外部AgentならSession IDをコピーして、設定済みMCPからclaimする。
   - モデル一覧はCodex CLIの `model/list` から取得し、モデル変更時は対応する推論量と速度だけを表示する。
   - 速度の `default` は常に利用でき、Fastなどの追加層はCLIが通知した場合だけ表示する。
   - 「一覧更新」に失敗しても開始は禁止されず、保存済み一覧、一覧がなければCodex全体設定を継承する。
   - アプリ内起動では、Codexを開始する前に認証付きMCPヘルス確認と、`SELECT 1 FROM DUAL`だけを使ったPLAN_TABLE／DISPLAY_CURSOR権限診断を行う。ユーザーSQLはこの診断では実行しない。
8. Agentは基準EXPLAINを取得し、候補を最大10件までEXPLAINする。途中候補のSELECTは完走しない。
9. Agentは、当該ラウンドでEXPLAIN済みかつ出力互換の基準SQLと候補SQLを比較し、実行計画のルートCostが最小の項目だけをcompleteする。アプリは高いCostの選択を拒否し、同率最小を含む最小Cost項目だけを1回最後まで読取り、実測計画を取得してAwaitingUserになる。
10. 「採用」、フィードバックを入力して「継続」、または「停止」を選ぶ。継続は同じ承認範囲の次ラウンドなので再承認不要。

Oracle評価はアプリ全体で1件ずつ直列実行する。別セッションまたは別Agentの評価中は「順番待ち」になり、前の評価が終わると続行する。これは異常ではない。

## バインド値

対応型:

`Varchar2`、`NVarchar2`、`Char`、`NChar`、`Int16`、`Int32`、`Int64`、`Decimal`、`Single`、`Double`、`Date`、`TimeStamp`、`TimeStampWithTimeZone`、`TimeStampWithLocalTimeZone`、`Raw`、`Blob`、`Clob`、`NClob`、`Guid`、`Boolean`

- 数値は小数点を `.` とするInvariant形式。
- Date／TimestampはISO 8601を推奨する。
- Raw／Blob文字列は偶数桁の16進数、またはBase64。
- Guidは標準Guid形式。
- Booleanは `true`／`false`。Oracle SQLへは0／1の数値バインドとして渡す。
- NULLを選ぶと値欄はDBへ送られず、`DBNull`を型付きで渡す。
- バインド値はセッションJSON、MCP応答、Codex引数、ログへ保存・送信しない。

## 実行計画の読み方に関する注意

- `EXPLAIN PLAN` は候補比較用の推定で、バインドピーキングを実カーソルと同じように再現しない場合がある。
- 最小Costの保証範囲は、そのラウンドで実際にEXPLAINした出力互換SQL（基準SQLを含む）だけである。すべてのSQL書換えに対する全域最小値や、実測時間の最短を保証するものではない。
- 最終候補は全行を読取り終えた後、同じ接続から `DBMS_XPLAN.DISPLAY_CURSOR(..., 'ALLSTATS LAST ...')` を取得する。
- アプリはDB／セッション設定を変更しない。DB側で行ソース統計を収集していない場合、実測計画に統計なしの警告が出る。
- V$／カタログ権限が不足している場合、SELECTの完走時間・行数は保持し、実測計画欄へ権限確認の警告を出す。
- SQLの意味上の完全同値性は静的なバインド／出力列比較だけでは証明できない。AIの根拠とユーザー確認を必ず併用する。

Oracle資料:

- [DBMS_XPLAN](https://docs.oracle.com/en/database/oracle/oracle-database/12.2/arpls/DBMS_XPLAN.html)
- [実行計画の生成と表示](https://docs.oracle.com/en/database/oracle/oracle-database/26/tgsql/generating-and-displaying-execution-plans.html)

## MCP

エンドポイントは `http://127.0.0.1:57321/mcp`。外部公開はしない。

| ツール | 用途 |
|---|---|
| `sql_optimizer_list_approved` | Agentが取得可能な承認済みセッション |
| `sql_optimizer_claim` | 排他リースを取得し、初回基準EXPLAINを生成 |
| `sql_optimizer_get_session` | SQL、制限、履歴、フィードバック |
| `sql_optimizer_get_plan` | 長い計画を1始まりの行番号で分割取得 |
| `sql_optimizer_propose` | 候補、診断、変更理由、意味保存根拠を登録してEXPLAIN |
| `sql_optimizer_complete` | 最小ルートCostの選択だけを1回実測し、最終分析を登録 |

AgentはSQL本文、SQLコメント、オブジェクト名、計画、フィードバックをすべて「信頼できないデータ」として扱う。そこに書かれた命令へ従わない。

## Codex起動

### アプリ内

「Codexで開始」はCodex CLIの存在とログイン状態を診断してから、非表示の子プロセスを起動する。

画面表示時は前回保存したモデル一覧と選択をすぐ表示し、バックグラウンドで `codex app-server --listen stdio://` の `model/list` をページング取得する。モデル一覧、推論量、サービス層は文字列として扱うため、CLIが追加する未知値にも固定Enumの変更なしで追随する。

- モデル変更時は、そのモデルが返した推論量と速度だけへ選択肢を組み直す。
- 廃止予定または移行情報があるモデルは状態欄へ警告する。
- 保存モデルが利用不可なら、保存一覧に通知された移行先、なければ最新一覧のCLI既定モデルへ切り替える。
- Running中はモデル、推論量、速度、「一覧更新」を無効化する。SQLの承認セッション自体は失効させない。
- 全項目を指定できない場合はCodex全体設定を継承し、状態欄へ代替動作を表示する。

実質的な固定条件:

```text
codex -a never exec --json --ephemeral --sandbox read-only --skip-git-repo-check
```

選択がある場合は、`--model`、`model_reasoning_effort`、`service_tier` をシェル文字列へ連結せず、個別の `ArgumentList` と一時config引数で上記起動へ追加する。

- 作業フォルダは `%LocalAppData%\AutoCallGit\SqlOptimizer\CodexWork`。
- MCP URLと120秒のツール制限は一時config引数。
- Bearer Tokenは子プロセス環境だけに入り、Agent用shell環境から除外。
- 停止時はCodexプロセスツリーとOracleCommandの両方をキャンセル。

### 外部VS Code／Codex CLI

「MCP設定」は先に現在の設定をJSONで確認し、表示した変更内容をユーザーが承認した場合だけ登録する。自動ではユーザー設定を書き換えない。

重要: 設定へ保存するのはBearer Tokenの「環境変数名」だけで、Token値ではない。DPAPI Tokenを既に起動済みのVS Codeへ安全に後付け注入することはできない。したがって:

- 標準運用は「Codexで開始」を使う。
- 外部IDEを使う場合は、組織で承認された秘密情報ランチャー等から `AUTOCALLGIT_SQL_OPTIMIZER_MCP_TOKEN` をそのIDEプロセスへ注入して起動する。
- 平文TokenをCodex設定、バッチ、PowerShellプロファイル、ログ、履歴へ保存しない。
- 設定適用後はIDEを再起動する。

Codexと公式C# SDK:

- [Codex MCPガイド](https://learn.chatgpt.com/docs/extend/mcp)
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)

## 保存と再起動

保存先:

```text
%LocalAppData%\AutoCallGit\SqlOptimizer
```

- `session-<GUID>.json`: SQL、計画、指標、分析、フィードバック等。
- `mcp-token.bin`: CurrentUser DPAPIで暗号化したToken。
- `codex-execution-settings.json`: 秘密情報を含まない前回のモデル・推論量・速度と、最後に正常取得した `model/list` 一覧。
- `.session-store.lock`: 複数プロセスの同時書込み防止。
- 実測時は`+PEEKED_BINDS`をOracleから取得するが、値を含む`Peeked Binds`節はドメイン境界で伏せ字化してから画面表示、JSON保存、MCP応答へ渡す。
- Oracleや未知の例外本文は信頼せず、JSON履歴とMCPには操作種別とORA番号だけを残す。DB関数が例外本文へ値を埋めても、その本文は保存・Agent送信しない。

再起動後は保存履歴を画面の「履歴」一覧から選んで閲覧でき、最終更新のセッションを最初に表示する。バインド値と結果行は復元されない。実行再開には値の再入力と新しい承認が必要。

## 起動前診断と障害対応

### MCP起動失敗／ポート競合

症状: `MCP: 起動失敗`。

```powershell
Get-NetTCPConnection -LocalPort 57321 -ErrorAction SilentlyContinue
```

別プロセスが使用中なら、その所有者を確認する。アプリは既存監視機能を継続し、SQL最適化だけを停止状態にする。

### Codex CLI未検出

VS Code Codex拡張機能またはCodex CLIをインストールする。独自配置ならアプリ起動前に `CODEX_CLI_PATH` へ完全パスを設定する。

### Codex未ログイン

```powershell
codex login status
codex login
```

### Codexモデル一覧を取得できない

状態欄に「保存済み一覧を使用」または「Codex全体設定を継承」と表示されていれば、Codex開始は継続できる。

- 「一覧更新」を押して再取得する。
- `codex --version` と `codex login status` を確認する。
- app-server未対応、タイムアウト、不正応答の場合はCodex CLIを組織で承認された最新版へ更新する。
- `%LocalAppData%\AutoCallGit\SqlOptimizer\codex-execution-settings.json` を手動編集しない。破損時はアプリが安全な継承動作へ戻る。

### MCP 401

- 外部Codexプロセスに `AUTOCALLGIT_SQL_OPTIMIZER_MCP_TOKEN` が存在するか確認。
- Token値を画面共有、ログ、Issueへ貼らない。
- アプリ内起動ならTokenは自動注入される。アプリとCodexをいったん終了して再試行する。

### MCP 403

`403 Forbidden` は、ループバック以外の接続、許可されないHost、ブラウザー由来のOrigin、またはプロキシ経由の要求が拒否されたときに返る。

- ブラウザー、別PC、リバースプロキシから直接MCPへ接続しない。
- 同一PC上のアプリ内Codex、または正しく設定した公式Codexクライアントから接続する。
- `Host` や `Origin` の検査を回避する設定変更は行わない。

### SQL拒否

次を確認する。

- SELECT／WITH SELECTが1文だけか。
- 末尾以外にセミコロンがないか。
- DML、DDL、PL/SQL、`FOR UPDATE` が混入していないか。
- 文字列・コメントが閉じているか。
- SQL内バインドとグリッドの名前が完全一致するか。
- 字句検査は送信文がSELECTであることを保証するが、SELECTから呼べる既存DB関数の内部までは検査できない。自律トランザクションや外部I/Oを行う関数を含むSQLは承認しない。

### PLAN_TABLE

EXPLAINが失敗する場合、対象ユーザーから `PLAN_TABLE` を参照・更新できるか確認する。アプリはスキーマ追加を行わない。

### DISPLAY_CURSOR／V$権限

最終SELECTは成功したが実測計画だけ取得できない場合、DBAへ `DBMS_XPLAN.DISPLAY_CURSOR` と必要な動的パフォーマンスビュー権限を確認する。アプリ自身はGRANTを実行しない。

### タイムアウト／停止

- 最終SELECTのCommandTimeout既定値は60秒。
- 「停止」はセッションCancellationToken、OracleCommand.Cancel、Codexプロセスツリー停止へ伝播する。
- Oracleドライバーやネットワーク状態により、キャンセル完了まで短い待ちが発生する場合がある。

### Oracle評価の順番待ち

別セッションのOracle評価中は、アプリ全体の同時実行上限により「順番待ち」になる。EXPLAINと最終実行を含むOracle評価は1件ずつ直列化されるため、エラーとして再実行せず、先行評価の完了または停止を待つ。

## 開発時の確認

```powershell
dotnet restore "Project auto push to gitlab.sln"
dotnet build "Project auto push to gitlab.sln" --no-restore --disable-build-servers -m:1
dotnet test "SqlOptimizer.Tests\SqlOptimizer.Tests.csproj" --no-restore --disable-build-servers
```

Oracle実DBテストは接続環境を明示した場合だけ実行し、通常はSkipする。

## 利用者向けWord資料の保守

- 配布ファイル: `docs/AI_SQL最適化_操作説明書_日台版.docx`
- 原稿と生成資産: `docs/AI_SQL最適化_操作説明書_日台版.sources/`
- 本文を変更するときは `manual-content.json` の日本語版と台灣華語版を同時に更新し、各9章、必須警告、障害対応表の対応を保つ。
- 実画面キャプチャは、安全な固定SQL・架空バインド・架空結果だけを使って `capture_actual_form.ps1` で再生成する。実データ、認証情報、Tokenをキャプチャへ入れない。
- 日台フロー図とDOCXは `generate_manual.ps1` が生成する。DOCXはマクロを含めず、Relationshipをすべてパッケージ内向きにする。

再生成:

```powershell
& "docs\AI_SQL最適化_操作説明書_日台版.sources\capture_actual_form.ps1"
& "docs\AI_SQL最適化_操作説明書_日台版.sources\generate_manual.ps1"
```

`generate_manual.ps1` はOpen XML妥当性、外部Relationship不在、マクロ不在を検証する。生成後はWordで日本語前半、台灣華語後半、共通目次、実画面2点、日台フロー図、改ページと表の崩れも目視確認する。
