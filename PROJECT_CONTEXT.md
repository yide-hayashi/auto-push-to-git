# Project Context: AutoCallGit

最終更新日: 2026-08-20  
分析基準コミット: `5c4c875` (`main`)  
対象ソリューション: `Project auto push to gitlab.sln`

## この文書の使い方

- 今後このリポジトリを扱うときは、最初にこのファイルを読む。
- 全ファイルの再走査は原則不要。変更対象に関係する「変更時の参照先」だけを確認する。
- 基準コミット以降に構造変更がある場合、または実コードとこの文書が矛盾する場合だけ、対象範囲を再確認して本書も更新する。
- `.Designer.cs` と `.resx` はWinForms画面の一部であり、単なる生成ノイズとして削除しない。
- `bin/`、`obj/`、`.vs/`、`*.user` は成果物・個人設定であり、Gitへ追加しない。

## 1. 一言でいうと

Windows通知領域に常駐し、次の三つの機能群をWinFormsへ集約した社内向け運用ツール。

1. Visual Studioで開いているGitプロジェクトを監視し、Visual Studio終了後にVS Codeを開いてローカルHTTPサービスへGit処理を依頼する。
2. OracleのSession、PGA、リソース使用量、SQL_ID、実行計画を照会し、参照履歴と実行計画をDBへ保存する。
3. ユーザー承認済みのOracle SELECTを、ローカルMCP経由のAI AgentでEXPLAIN比較し、選択候補だけを実測する。

重要: このリポジトリ自身は `git add`、`git commit`、`git push` を実行しない。Git処理の実体は、`127.0.0.1:3000-3100` 上の `/OpenGit` を提供する外部プロセスに委譲されている。

## 2. 技術構成とビルド

| 項目 | 現在の内容 |
|---|---|
| アプリ形式 | Windows Forms / `WinExe` |
| ソリューション | 1ソリューション、アプリ＋テストの2プロジェクト |
| Target Framework | `net8.0-windows` |
| Nullable | 有効 |
| Implicit Usings | 有効 |
| FileVersion | `0.0.2.0320` |
| AssemblyVersion | `0.0.1.115` |
| 直接NuGet依存 | `KTCC.Core 2.0.9`、`ModelContextProtocol.AspNetCore 1.3.0`、`System.Security.Cryptography.ProtectedData 8.0.0` |
| Framework参照 | `Microsoft.AspNetCore.App` |
| 主な推移依存 | `Dapper 2.0.123`、`Oracle.ManagedDataAccess.Core 3.21.50` |
| KTCC.Core取得元 | 端末側NuGet設定の社内フィード `https://nuget.ktgh.com.tw/api/v3/index.json` |
| 外部DLL | `C:\AP06BZ\EXE\C1.Win.C1TrueDBGrid.2.dll` |
| リモート | `https://github.com/yide-hayashi/auto-push-to-git.git` |

`C1.Win.C1TrueDBGrid.2` はプロジェクト参照に存在するが、現在のC#ソースには使用箇所がない。絶対パス参照なので、別PCではビルド移植性を損なう可能性がある。

`global.json`、リポジトリ内 `NuGet.Config`、`packages.lock.json` はない。SDK選択、社内NuGetフィード、依存解決は端末環境に依存する。

標準の確認コマンド:

```powershell
dotnet build "Project auto push to gitlab.sln" --no-restore --configuration Debug
```

2026-07-24の確認環境:

- SDK: `10.0.300`
- .NET 8 Runtime: `8.0.27`
- ビルド結果: エラー0、既存コード由来の警告116
- 主な警告: Nullable未初期化、Null参照の可能性、未使用変数、`string` と `object` の参照比較
- `SqlOptimizer.Tests` は通常テスト71件成功、Oracle実DBテスト1件は接続環境未設定のためSkip

## 3. ディレクトリとファイルの責務

| パス | 責務 |
|---|---|
| `Program.cs` | WinForms初期化と `frm_SessionGrid` 起動 |
| `frm_SqlOptimizer.*` | AI SQL最適化の入力、Codexモデル・推論量・速度選択、承認、反復履歴、計画・分析・結果プレビュー |
| `frm_SessionGrid.cs` | メイン画面、Oracle照会、プロセス監視、VS Code起動、HTTP通知、履歴・実行計画保存 |
| `frm_SessionGrid.Designer.cs` | メイン画面レイアウトとイベント配線 |
| `frm_SessionGrid.resx` | 通知アイコンとステータスメニュー画像 |
| `frm_Sql_idLoggingGrid.*` | SQL_ID参照履歴一覧 |
| `frm_AddWatchProgressList.*` | DB監視対象プログラムの一覧・追加 |
| `frmText.*` | 実行計画を表示する単純なテキスト画面 |
| `model/cls_Datamodel.cs` | DapperがOracle結果を格納するDTO群 |
| `cls/cls_GetWindowsData.cs` | SQL履歴共有、`inf100` 入出力、netstat解析、ローカルHTTP GET |
| `cls/cls_reg.cs` | 正規表現の薄いラッパー |
| `extension/KTCCextension.cs` | 院区コードからKTCC接続先への変換、デバッグSQL生成 |
| `extension/Extension.cs` | 民国暦・時刻・Regex・CLOB文字列分割の拡張メソッド |
| `Properties/Resources.*` | プロジェクト標準リソース。data / metadataともに現在0件 |
| `SqlOptimizer/Application` | 機能のComposition RootとWinForms Controller |
| `SqlOptimizer/Core` | SQL検証、状態遷移、リース、候補上限、サービス契約 |
| `SqlOptimizer/Oracle` | KTCC接続、型付きバインド、EXPLAIN、最終SELECT、DISPLAY_CURSOR |
| `SqlOptimizer/Mcp` | ループバックMCPホスト、Bearer認証、固定6ツール |
| `SqlOptimizer/Codex` | DPAPI Token、CLI検出・起動、app-server `model/list`、動的能力選択、JSONL、安全なMCP設定確認 |
| `SqlOptimizer/Persistence` | 機密値を除いたセッションJSONとCodex実行選択・モデル一覧の原子的保存 |
| `SqlOptimizer.Tests` | Core、Persistence、Codex境界、偽Oracle結合テスト |
| `docs/SQL_OPTIMIZER_AGENT.md` | 利用手順、安全境界、外部Agent設定、障害対応 |
| `docs/AI_SQL最適化_操作説明書_日台版.docx` | 日本語／台灣華語の利用者向け画面付き操作説明書 |
| `docs/AI_SQL最適化_操作説明書_日台版.sources` | Word原稿JSON、実フォーム安全キャプチャ、日台フロー図、再生成・検証スクリプト |

## 4. 現在のアーキテクチャ

レイヤードアーキテクチャではなく、WinFormsコードビハインド中心の構造。

```text
Program
  └─ frm_SessionGrid
       ├─ UIイベントと画面状態
       ├─ KTCC.Core / DapperによるOracle SQL
       ├─ LoggingDataによるSQL_ID履歴
       ├─ dataStoreによるGitディレクトリ探索
       ├─ devenv.exe終了監視
       ├─ cmd.exe経由のVS Code起動
       ├─ cls_GetWindowsDataによるnetstat解析とHTTP通知
       ├─ frm_Sql_idLoggingGrid
       ├─ frm_AddWatchProgressList
       └─ frmText
```

Repository層、Service層、DI、設定クラス、インターフェース、構造化ログはない。メインフォームがUI、DB、OSプロセス、ファイルシステム、HTTPの責務を同時に持つ。

## 5. 起動と終了

### 起動

`Program.cs:8-15` から `frm_SessionGrid` を起動し、`frm_SessionGrid.cs:31-91` で次を行う。

1. タスクバーから隠し、フォーム自体も非表示にする。
2. 通知領域アイコンを表示する。
3. `LoggingData.InstanceData()` で `inf100` から過去のSQL_ID履歴を復元する。
4. `D:\VB\2008` 配下のGitプロジェクトを探索する。
5. 10秒周期の `devenv.exe` 監視Taskを開始する。
6. KTGH00とHPK210のSession、PGA、Session使用量を各1回ずつバックグラウンド取得する。

通常の初期状態は「画面非表示、通知領域に常駐」。

### 表示と終了

- 通知アイコンのダブルクリック: メイン画面を表示する (`frm_SessionGrid.cs:391-399`)。
- ウィンドウの×: 終了をキャンセルし、通知領域へ戻す (`401-414`)。
- 通知領域メニューのClose: Visual Studio監視を停止し、フォームを閉じる (`372-389`)。

Session監視とPGA監視には共通の終了用CancellationTokenがなく、Close時に完全停止が保証されない。

## 6. 機能A: Visual Studio終了後のGit連携

実装の中心は `frm_SessionGrid.cs:263-364`、`:802-880` と `cls/cls_GetWindowsData.cs:110-203`。

### フロー

1. `dataStore.getchildpathHasGitdir("D:\VB\2008")`
   - サブディレクトリを再帰探索する。
   - `.git` を含むディレクトリを `dataStore.projectpath` に保存する。
   - `old`、`upgrade`、`測試用`、`封存` などを正規表現で除外する。
2. `per10sGetDevList()`
   - 10秒ごとに `Process.GetProcessesByName("devenv")` を取得する。
   - Visual Studioのウィンドウタイトルからプロジェクト名を抽出する。
   - 抽出名と `.git` パスを照合し、`ThreadingData` を作る。
3. `watchingProcess(ThreadingData)`
   - 500ms周期でVisual Studioプロセス終了を待つ。
4. Visual Studio終了後
   - `cmd.exe` を非表示で起動する。
   - `cd` と `code .` を標準入力へ送ってVS Codeを開く。
5. 3秒待った後、最大10回 `code` プロセスを探す。
6. `netstat.exe -ano` を実行し、3000-3100番ポートを列挙する。
7. 列挙した全ポートへ次のGETを送る。

```text
http://127.0.0.1:{port}/OpenGit?pgm={projectName}
```

### 境界

- C#側が行うのは `/OpenGit` 通知まで。
- commitメッセージ生成、stage、commit、pushの実装はこのリポジトリにない。
- `/OpenGit` の提供元、API仕様、認証、実際のGit処理は外部依存。

### 環境依存

- 検索ルートは `D:\VB\2008` に固定。
- Visual Studioのプロセス名は `devenv` に固定。
- VS Code CLIの `code` がPATHに必要。
- `netstat.exe` と `cmd.exe` が必要。
- ローカルサービスは3000-3100番で待機している前提。

## 7. 機能B: Oracle監視とSQL診断

### 接続先マッピング

`extension/KTCCextension.cs:53-74` の `SettingConnectWithHspArea` が文字列を `KTCC.Core.ConnectHost` へ変換する。

各DB処理は `KTConnectionController` を `using` で生成し、院区設定後に `DoSql<T>` または `DoSql` を同期実行する。

| 入力別名 | 接続先 |
|---|---|
| `KTGH00`、`KTGH`、`SX`、`SA` | `ConnectHost.KTGH00` |
| `HPK210`、`HPK`、`TA` | `ConnectHost.HPK210` |
| `TSHIS`、`TS` | `ConnectHost.TSHIS` |

未知値は例外にせず未接続のまま戻る。接続文字列や資格情報はリポジトリ内に存在せず、`KTCC.Core` と実行環境側に依存する。

### 読取フロー

| 機能 | Oracleソース | DTO | 表示先 |
|---|---|---|---|
| DB Session | `v$session` | `cls_v_session` | KTGH00 / HPK210 Session Grid |
| PGA | `v$pgastat` | `cls_PAGData` | KTGH00 / HPK210 PGA Grid |
| Session等の使用量 | `v$resource_limit` | `cls_SessionUsingData` | KTGH00 / HPK210 Session使用量Grid |
| SQL詳細 | `v$sql` | `cls_sql_idData` | `Gridsql_stauts` |
| 実行計画 | `DBMS_XPLAN.DISPLAY_CURSOR` | `cls_DBMS_XPLAN_DISPLAY_CURSOR` | `frmText` |

Oracle動的パフォーマンスビューと `DBMS_XPLAN` を読む権限が必要。

### Session監視

- 実装: `frm_SessionGrid.cs:198-262`、`:416-461`
- 監視対象プログラムは `inf100` の `dtflg='watch_Progress'` から取得する。
- KTGH00とHPK210を別Taskで更新する。
- 更新間隔の初期値は2500ms。
- 取得後は `Control.Invoke` でDataGridViewへ反映する。

### SQL_ID詳細

- Session行のダブルクリック:
  - KTGH00: `frm_SessionGrid.cs:463-491`
  - HPK210: `569-596`
- 手動入力: `543-567`
- `v$sql` の全文、実行回数、CPU秒、経過秒、buffer gets、最終ロード時刻を取得する (`500-534`)。
- SQL_ID参照履歴をメモリと `KTGH00.inf100` へ保存する。

### 実行計画

- SQL詳細行のダブルクリックで `DBMS_XPLAN.DISPLAY_CURSOR` を実行する (`598-648`)。
- 結果を `frmText` へ表示する。
- 既存確認後、`KTGH00.ino013_sub` へSQL全文と実行計画を保存する (`651-725`)。
- SQL全文と実行計画は3800文字単位に分割し、`TO_CLOB(...) || ...` のSQL断片へ変換する。

## 8. DB永続化の対応

### `inf100`: SQL_ID参照履歴

`LoggingData.InsertLogToDB` (`cls/cls_GetWindowsData.cs:21-60`)。

| 列 | 保存内容 |
|---|---|
| `dtflg` | 固定値 `Session` |
| `item1` | 院区 |
| `itemnme1` | SQL_ID |
| `item2` | PID |
| `itemnme2` | `RunningExeName + "$" + client_info` |
| `upop` | 固定値 `SUPER` |
| `updte` | 民国日付 |
| `uptim` | `HHmmss` |

起動時の復元は `LoggingData.InstanceData` (`61-94`)。保存時に結合した `$client_info` は復元時に分解されない。

### `inf100`: 監視対象プログラム

`frm_AddWatchProgressList.cs:48-106`。

| 列 | 保存内容 |
|---|---|
| `dtflg` | 固定値 `watch_Progress` |
| `item1` | 固定値 `His` |
| `itemnme1` | 小文字化したexe名 |

追加時はKTGH00、HPK210、TSHISの3接続先へ順番にINSERTする。一覧取得と10分キャッシュはKTGH00だけを参照する。

### `ino013_sub`: 実行計画履歴

主な保存項目:

- 院区、SQL_ID、SQL全文
- CPU、実行回数、buffer gets、module
- 実行計画
- 実行日・時刻、参照日・時刻
- client_info

存在確認とINSERTは別接続・別処理であり、原子的ではない。

## 9. 画面構成

### `frm_SessionGrid`

`frm_SessionGrid.Designer.cs:29-398`

- 上部: Session監視チェック、更新間隔、SQL_ID入力、院区選択、取得ボタン
- SQL詳細: `Gridsql_stauts`
- タブ1: `KTGH00_session`
- タブ2: `HPK210_session`
- タブ3: KTGH00 / HPK210のPGAとSession使用量
- 下部メニュー:
  - `Sql_id Loggin`
  - `Add Watching Progress`
- タイトル: `AutoCallGit`

### `frm_Sql_idLoggingGrid`

- `LoggingData.cls_Loggings` を直接DataGridViewへバインドする。
- 履歴行のダブルクリックで、保持している `frm_SessionGrid` 参照を通じてSQL詳細を再取得する。
- フォーム間連携はイベントやインターフェースではなく、公開フィールドによる直接参照。

### `frm_AddWatchProgressList`

- `inf100` の監視対象一覧を表示する。
- OSの実行中プロセス名をComboBoxへ列挙する。
- 選択名へ `.exe` を付けて3院区へ登録する。
- 追加後はプロセス一覧だけを更新し、監視対象Gridはその場で再取得しない。

### `frmText`

- 実行計画全文を表示する複数行TextBoxだけを持つ。

## 10. 共有状態と非同期処理

WinForms `Timer` は使わず、`Task.Run` + `while (true)` + `Thread.Sleep` でポーリングする。

| 状態 | 場所 | 用途 |
|---|---|---|
| `closeflg` | `frm_SessionGrid.cs:25` | 通知領域へ隠す操作と完全終了の区別 |
| `publicData.threadingDatas` | `802-810` | 検出したVisual Studio監視対象 |
| `publicData.Getdevforpre10sec` | `802-810` | Visual Studio探索継続フラグ |
| `publicData.RunningThreading` | `802-810` | 個別の終了監視Task |
| `dataStore.projectpath` | `833-879` | 発見した `.git` パス |
| `dataStore.tasksPath` | `833-879` | 再帰探索Task |
| `LoggingData.cls_Loggings` | `cls/cls_GetWindowsData.cs:17-19` | SQL_ID履歴 |
| `cls_GetWindowsData.listport` | `110-176` | 3000-3100番ポート一覧 |
| `cls_inf100Data` | `frm_AddWatchProgressList.cs:24-28` | 監視対象の10分キャッシュ |

CancellationToken、BindingList、ConcurrentCollectionは使われていない。通常の `List<T>` とboolを共有し、一部だけ `lock` している。

## 11. DTO

`model/cls_Datamodel.cs` の型は、ほぼすべてDapperの列名対応用。

| 型 | 内容 |
|---|---|
| `cls_v_session` | Oracle Session |
| `cls_PAGData` | PGA統計。実装上の綴りは `PAG` |
| `cls_SessionUsingData` | リソース使用量 |
| `cls_sql_idData` | `v$sql` 詳細 |
| `Insertino013_subDtlfgExplain_PLAN` | 実行計画保存用。`cls_sql_idData` を継承 |
| `cls_LoggingForDB` | SQL_ID参照履歴 |
| `ino013_sub` | 実行計画の存在確認 |
| `inf100Data` | 監視対象プログラム |
| `cls_DBMS_XPLAN_DISPLAY_CURSOR` | 実行計画1行 |

特徴:

- 数値と日時も含め、ほぼすべて `string`。
- Nullable有効なのに非Nullableプロパティを初期化していない。
- 派生型 `Insertino013_subDtlfgExplain_PLAN.hsp` が基底型の同名プロパティを隠す。
- `inf100Data` は `LoggingData` 内にも別形状で定義されている。

## 12. 拡張・補助メソッド

### `extension/KTCCextension.cs`

- `SettingConnectWithHspArea`: 院区別名から接続先を決める。広く使用中。
- `GetDebugsqlForDaaper`: SQLパラメータを文字列置換するデバッグ補助。現在は未使用。

### `extension/Extension.cs`

このファイルだけ名前空間が `INOP1101.cls` で、現プロジェクトの名前空間と一致しない。利用側は `using INOP1101.cls` を追加している。

実際に本プロジェクトで使用中:

- `民國日期`
- `timer`
- `insertOver4000TextForCSharp`

定義されているが現在未使用:

- `西元日期YYYYMMDD`
- `民國日期hasSlash`
- `民國年月`
- `timerHasColon`
- `timerHM`
- `民國轉西元`
- `民國YM轉西元`
- `西元str轉民國`
- Regex関連の各メソッド
- `DealwithCLOBForInsertDB`

### `cls/cls_reg.cs`

- `reg`: 最初の正規表現一致。Git探索の除外判定で使用。
- `RegReplace`: Visual Studioタイトル整形で使用。
- `RegSet`、`RegSetReturnString`: 現在未使用。

## 13. コードの書き方と慣習

- 典型的なイベント駆動WinFormsコードビハインド。
- SQLは複数行を `strsql +=` で連結する。
- Dapperパラメータは使う箇所と文字列埋め込みの箇所が混在する。
- 日付・時刻は民国暦の文字列で扱う箇所がある。
- 中国語、日本語、英語のコメントと識別子が混在する。
- `Stauts`、`PAG`、`Loggin`、`Progress` など綴り揺れが多い。検索時は実装上の綴りを使う。
- publicな可変フィールド、staticな共有状態が多い。
- 型名は `cls_` 接頭辞、DTOのプロパティはDB列名に近い小文字が中心。
- `using` の重複や未使用、別プロジェクト由来の名前空間が残る。
- 例外処理は一貫せず、握りつぶす箇所と未処理の箇所が混在する。

既存の書き方へ小修正を加える場合は、Dapperの列名マッピングとDesignerイベント名を壊さないこと。大きな改善ではUI、DB、OS操作を段階的に分離する。

## 14. 既知の重要リスク

優先度の高い順。

1. **バックグラウンドTaskからWinForms Controlを直接参照**
   - `WatchModel.Checked`、`watchingUseing.Checked`、`txtWatchTime.Text` をUIスレッド外から読む。
   - `InvalidOperationException` や不定動作の可能性。
2. **監視Taskの重複と終了漏れ**
   - CheckedChangedのたびに新規Taskを作る。
   - OFF→ONが速いと旧Taskが再開条件を満たし、重複監視になり得る。
   - Close時にSession/PGA監視を確実に止めない。
3. **HPK210 PGA Taskだけ停止待ちリストへ未追加**
   - `frm_SessionGrid.cs:744-755`。
4. **実行計画の接続先が行データではなくComboBox依存**
   - `frm_SessionGrid.cs:598-614`。
   - HPK210行を選んでもComboBoxがKTGH00なら誤った院区へ問い合わせる。
5. **Session履歴の重複判定が `string == object`**
   - `frm_SessionGrid.cs:469`、`:575`。
   - 値比較にならず重複保存し得る。
6. **監視対象0件で例外**
   - `frm_SessionGrid.cs:213-216` の末尾カンマ削除。
7. **SQL文字列への直接埋め込み**
   - 監視プログラム名、院区、client_info、module、CLOB断片。
   - 引用符、SQL破損、SQL injection、NLS・文字コード依存のリスク。
8. **CLOB分割が文字数基準**
   - Oracle制限はbyte基準になり得る。
   - マルチバイト文字やサロゲートペアを安全に扱えない。
   - `List.IndexOf` による区切り判定は重複chunkで誤る。
9. **共有 `List<T>` の競合**
   - `tasksPath`、`projectpath`、`threadingDatas`、`listport`、SQL履歴を複数Taskから利用。
   - lock範囲が列挙と更新で統一されていない。
10. **再帰探索のTask管理**
    - `tasksPath.Add` が同期されない。
    - 起動側の `WaitAll(ToArray())` はその時点のsnapshotしか待たない。
    - 権限拒否、junction循環、消失ディレクトリの処理がない。
11. **`.git` 判定が部分一致**
    - `.github` を誤認し得る。
    - `.git` がファイルになるworktreeは検出できない。
12. **VS Code起動コマンド**
    - `cd D:` / `cd path` ではドライブ切替を保証しない。`cd /d` または `WorkingDirectory` が必要。
    - パスをcmdへ直接連結する。
    - hiddenなcmdが残留する可能性。
13. **全3000-3100番ポートへ副作用GET**
    - プロセス名で絞り込まず、無関係なローカルサービスへ `/OpenGit` を送る可能性。
    - `pgm` をURLエンコードしていない。
14. **DB保存失敗を通知しない**
    - `LoggingData.InsertLogToDB` は例外を握りつぶす。
    - メモリへ先に追加するため、DB失敗後も再試行されにくい。
15. **設定がハードコード**
    - `D:\VB\2008`、院区別名、ポート範囲、SQL、テーブル名がコード内。
16. **移植性**
    - アプリ本体は.NET 8へ更新済みだが、C1 DLLは2006年版の絶対パス参照。
    - SDK固定、NuGet設定、依存ロックがなく、新規端末では社内フィード設定も必要。
17. **既存監視機能の自動テスト不足**
    - AI SQL最適化機能には.NET 8テストプロジェクトがある。
    - 一方、従来のDB、OSプロセス、HTTP、UI監視処理は密結合のままで、単体テスト境界が少ない。

## 15. 変更時の参照先

全件走査の代わりに、目的別に次だけを読む。

| 変更目的 | 最初に読む場所 |
|---|---|
| 起動・終了・通知領域 | `Program.cs`、`frm_SessionGrid.cs:25-91`、`:366-414` |
| Visual Studio / VS Code連携 | `frm_SessionGrid.cs:263-364` |
| Gitフォルダ探索 | `frm_SessionGrid.cs:833-879`、`cls/cls_reg.cs` |
| ローカル `/OpenGit` 呼出 | `cls/cls_GetWindowsData.cs:110-203` |
| Session監視 | `frm_SessionGrid.cs:198-262`、`:416-461`、`model/cls_Datamodel.cs:9-28` |
| PGA・使用量 | `frm_SessionGrid.cs:92-196`、`:728-793` |
| SQL_ID詳細 | `frm_SessionGrid.cs:463-596` |
| 実行計画 | `frm_SessionGrid.cs:598-725` |
| SQL履歴 | `cls/cls_GetWindowsData.cs:17-108`、`frm_Sql_idLoggingGrid.cs` |
| 監視対象追加 | `frm_AddWatchProgressList.cs` |
| 院区接続先 | `extension/KTCCextension.cs:48-74` |
| DTO・Dapper列対応 | `model/cls_Datamodel.cs` |
| 民国暦・CLOB処理 | `extension/Extension.cs` |
| 画面配置・イベント配線 | 対象フォームの `.Designer.cs` |
| アイコン・画像 | 対象フォームの `.resx` と `.Designer.cs` |
| NuGet・Framework・DLL | `Project auto push to gitlab.csproj` |
| AI SQL最適化画面・起動配線 | `frm_SqlOptimizer.*`、`SqlOptimizer/Application`、`Program.cs` |
| SELECT安全検査・状態遷移 | `SqlOptimizer/Core` |
| EXPLAIN・最終Oracle実行 | `SqlOptimizer/Oracle` |
| MCP公開範囲・認証 | `SqlOptimizer/Mcp` |
| Codex CLI・MCP設定 | `SqlOptimizer/Codex` |
| 最適化履歴JSON | `SqlOptimizer/Persistence` |
| 利用者向け操作説明書 | `docs/AI_SQL最適化_操作説明書_日台版.docx`、同 `.sources`、`docs/SQL_OPTIMIZER_AGENT.md` |

## 16. `frm_SessionGrid.resx` の基準状態

2026-07-24時点で、マージコミット `5c4c875` が画像データ内へ競合記号と別画像を180行残していた。修正後の正しい状態は親コミット `f4fa313` の同ファイルと一致する。

正しいリソース構成:

- data 2件
  - `notifyIcon.Icon`
  - `toolStripSplitButton1.Image`
- metadata 4件
- `<<<<<<<`、`=======`、`>>>>>>>` は存在しない

Designerが開けない場合の最初の確認:

```powershell
Select-String -Path "frm_SessionGrid.resx" -Pattern "^(<<<<<<<|=======|>>>>>>>)"
[xml](Get-Content "frm_SessionGrid.resx" -Raw)
dotnet build "Project auto push to gitlab.sln" --no-restore
```

## 17. 推奨する将来の分離順

大規模改修を行う場合は一度に書き換えず、次の順で境界を作る。

1. TaskポーリングをCancellationToken付きの監視サービスへ分離する。
2. WinForms Controlの値をUIスレッドでsnapshot化し、バックグラウンド処理へ渡す。
3. Oracle照会と保存をRepositoryへ分離し、SQLをパラメータ化する。
4. Gitプロジェクト探索、IDE監視、VS Code起動、HTTP通知を別サービスへ分離する。
5. static `List<T>` を同期可能な状態管理またはBindingListへ置き換える。
6. ハードコード値を設定ファイルへ移す。
7. 外部I/O境界へインターフェースを置き、テストを追加する。
8. .NETのサポート対象バージョンへ更新し、未使用C1参照を確認して除去する。

## 18. この文書を更新する条件

次の変更をしたときは、同じコミットでこのファイルも更新する。

- 起動フロー、常駐方法、終了方法
- フォーム追加・削除・主要イベント変更
- DB接続先、テーブル、動的ビュー、DTO列の変更
- Git/IDE/VS Code/HTTP連携方法の変更
- Framework、NuGet、外部DLLの変更
- static共有状態やTask管理方法の変更
- AI SQL最適化の画面項目、安全境界、承認範囲、状態遷移、障害表示を変更したときは、日台Word原稿と生成済みDOCXも更新する
- 上記の既知リスクを解消したとき

## 19. AI SQL最適化機能

### 起動と責務境界

`Program.cs` は既存メイン画面を作る前に `SqlOptimizerApplicationRuntime` を構成し、MCPホストを開始する。MCP開始に失敗しても既存監視画面は起動し、SQL最適化画面へ失敗理由を表示する。終了時はCodex子プロセス、MCPホストの順で停止する。

`frm_SessionGrid` と通知領域の両方に「AI SQL最適化」入口がある。フォームは入力・表示とイベントだけを持ち、処理は `SqlOptimizerUiController`、状態管理は `SqlOptimizationService`、Oracleは `OracleSqlPlanEvaluator` へ分離した。

### 状態と承認

状態は次の順で遷移する。

```text
Draft -> Approved -> Running -> AwaitingUser -> Accepted
                            \                 \-> Approved (Continue / next round)
                             \-> Stopped
                             \-> Error
```

- 承認対象は接続先、SQLハッシュ、バインド定義、最適化目標、実行制限。
- SQL、接続先、バインド表、最適化目標、制限を画面で変更すると、以前の承認セッションを停止してDraftへ戻す。
- 1ラウンドの候補は基準SQLを除き最大10件。
- リースは30分。別Agentの二重claimを拒否する。
- 停止はセッション別CancellationTokenを通じてOracleCommandとCodex子プロセスへ伝播する。
- 再起動後のApproved／Running／AwaitingUserはDraftとして復元し、バインド値の再入力と承認を必須にする。

### Oracle境界

- 接続先は `KTGH00`、`HPK210`、`TSHIS` のみ。
- 字句検査は単一の `SELECT` または `WITH ... SELECT` だけを許可し、複数文、DML、DDL、PL/SQL、`FOR UPDATE` を拒否する。
- 基準と途中候補は専用接続、一意の30文字以内 `STATEMENT_ID`、`PLAN_TABLE`、`DBMS_XPLAN.DISPLAY` を使い、最後にロールバックする。
- EXPLAINと最終SELECTを含むOracle評価は、アプリ全体の共有ゲートで1件ずつ直列実行する。他セッションは取消可能なキューで「順番待ち」になる。
- 出力列は0行メタデータで名前・順序・Oracle型・NULL可否・サイズ・数値精度／桁を比較し、不一致候補は最終実行不可。
- 最終候補だけを読取専用トランザクションで最後まで読取り、画面には先頭200行（画面設定範囲1～1000）だけを保持する。
- `DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST +PEEKED_BINDS +PREDICATE +ALIAS +OUTLINE')` は同じ接続で取得する。
- `Peeked Binds`節はPlanSnapshot生成時に伏せ字化し、バインド値が画面、JSON、MCPへ流れないようドメイン境界でも防御する。
- DB／セッション設定は変更しない。DB側で行ソース統計が収集されていない場合は警告を返し、設定変更は提案文だけに留める。
- EXPLAINはバインドピーキングを再現しない場合があるため、最終判断は実測計画を優先する。
- 字句検査は送信するトップレベル文をSELECTへ限定する。既存DB関数内部の自律トランザクションや外部I/Oまでは判定できないため、呼出関数を含むSQLはユーザー確認も安全境界になる。

### MCPとCodex

- エンドポイント: `http://127.0.0.1:57321/mcp`
- 認証: インストール単位の32-byte Bearer Token。`%LocalAppData%\AutoCallGit\SqlOptimizer\mcp-token.bin` へCurrentUser DPAPIで暗号化。
- MCPはHostとRemote IPの両方をloopbackへ限定し、Origin付きブラウザー要求を拒否する。
- 非loopback、許可されないHost、ブラウザーOrigin、プロキシ経由の要求は `403 Forbidden` とする。運用でHost／Origin検査を無効化せず、同一PCのアプリ内Codexまたは正式に設定したCodexクライアントを使う。
- 公開ツールは次の6件だけ。
  - `sql_optimizer_list_approved`
  - `sql_optimizer_claim`
  - `sql_optimizer_get_session`
  - `sql_optimizer_get_plan`
  - `sql_optimizer_propose`
  - `sql_optimizer_complete`
- `sql_optimizer_list_approved` は開始可能なApprovedと、ユーザー判断待ちのAwaitingUserだけを列挙する。
- Draft、Accepted、Stopped、Errorのセッションは、IDを知っていてもMCPから取得不可。
- MCP応答とCodex JSONL縮約にはバインド値、結果行、資格情報、Tokenを含めない。
- Oracle／未知の例外本文も信頼せず、履歴とMCPへは固定の操作名とOracleエラー番号だけを返す。DB側が例外文へ値を埋めても保存・Agent送信しない。
- 画面上部2段目の枠付き「Codex実行設定」でモデル、推論量、速度を選ぶ。モデル一覧と対応能力は、保存済み一覧を先に表示してから `codex app-server --listen stdio://` のページング対応 `model/list` でバックグラウンド更新する。固定モデル一覧や固定Enumは持たない。
- モデル変更時は対応する推論量とサービス層だけを表示し、速度の `default` は常に用意する。追加のFast層などはCLIが返した場合だけ表示する。廃止・移行情報は状態欄へ通知する。
- 一覧取得に失敗した場合は保存済み一覧、一覧がなければCodex全体設定を継承し、Codex開始自体は阻止しない。Running中だけ三つの選択欄と一覧更新を無効化し、SQL承認は失効させない。
- WinForms起動は `codex -a never exec --json --ephemeral --sandbox read-only --skip-git-repo-check` をArgumentListで実行し、選択済み `--model`、`model_reasoning_effort`、`service_tier` とMCPを一時設定、Tokenを子プロセス環境だけへ渡す。Agent用shell環境からTokenを除外する。
- Codex子プロセス起動前に認証付きMCPヘルス確認と、`SELECT 1 FROM DUAL`だけを使うOracle PLAN_TABLE／DISPLAY_CURSOR権限診断を行う。診断ではユーザーSQLを実行しない。
- MCP設定画面は `codex mcp get ... --json` で現状を確認し、ユーザー確認後だけadd／replaceする。Token値はCodex設定へ保存しない。

### 保存

保存先は `%LocalAppData%\AutoCallGit\SqlOptimizer`。`session-<GUID>.json` を一時ファイル経由で原子的に置換し、プロセス間ロックも使う。`codex-execution-settings.json` は秘密情報を含まない前回のモデル・推論量・速度と最後に正常取得したモデル一覧を原子的に保存する。内部のCodex `models_cache.json` は参照しない。

保存するのはSQL、計画、指標、AI分析、エラー、時刻、フィードバック、バインドの名前・型・サイズだけ。次は保存しない。

- バインド値とNULL状態
- 結果プレビュー行
- Bearer Token平文
- Codexの生JSONL、ツール引数、標準出力

運用と障害対応の詳細は `docs/SQL_OPTIMIZER_AGENT.md`、利用者向けの画面手順は `docs/AI_SQL最適化_操作説明書_日台版.docx` を読む。

Word資料は `docs/AI_SQL最適化_操作説明書_日台版.sources/manual-content.json` を原稿とし、同フォルダの `capture_actual_form.ps1` で安全な架空データの実フォーム2状態、`generate_manual.ps1` で日台フロー図とDOCXを再生成する。本文変更時は日本語／台灣華語の両方を更新し、生成時のOpen XML妥当性、外部Relationship不在、マクロ不在に加えてWordでの目視確認も行う。
