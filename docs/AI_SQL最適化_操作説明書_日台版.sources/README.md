# AI SQL最適化 操作説明書の生成元

このフォルダは `../AI_SQL最適化_操作説明書_日台版.docx` の再生成資産です。Wordファイルを直接編集せず、原稿と画像を更新してから生成スクリプトを実行します。

## ファイル

| ファイル | 役割 |
|---|---|
| `manual-content.json` | 共通目次、日本語9章、台灣華語9章、警告、障害対応表、フロー図の文言 |
| `capture_actual_form.ps1` | 実際の `frm_SqlOptimizer` を安全な固定ダミーデータで描画し、Draft／AwaitingUserのPNGを生成 |
| `generate_manual.ps1` | 日台フロー図を生成し、4画像を内包したDOCXをOpen XMLとして作成・検証 |
| `images/actual-input-draft.png` | Draft入力状態の実フォームキャプチャ |
| `images/actual-awaiting-user.png` | AwaitingUser結果状態の実フォームキャプチャ |
| `images/flow-ja.png` | 日本語フロー図 |
| `images/flow-zh-tw.png` | 台灣華語フロー図 |

## 再生成

リポジトリルートのWindows PowerShellから実行します。

```powershell
& "docs\AI_SQL最適化_操作説明書_日台版.sources\capture_actual_form.ps1"
& "docs\AI_SQL最適化_操作説明書_日台版.sources\generate_manual.ps1"
```

フォーム表示に変更がなければ、本文とフロー図だけを更新するため2行目だけを実行できます。`capture_actual_form.ps1` は一時プロジェクトをOSの一時フォルダへ作り、既存プロジェクトを参照して実フォームをオフスクリーン描画し、終了時に一時ファイルを削除します。既存のフォーム、csproj、テストを編集しません。

## 保守ルール

- 本文の部見出し `第I部 日本語版` と `第II部 台灣華語版` は境界抽出に使うため変更しません。
- 各言語9章、共通目次、必須警告、障害対応表、実フォーム2点、日台フロー図の構成を保ちます。
- UIの項目、状態、承認範囲、安全境界、保存内容、エラー表示を変更した場合は、日本語と台灣華語を同時に更新します。
- 画面キャプチャへ渡すのは、固定の架空SQL、架空バインド、架空Session ID、架空計画、架空結果だけです。実DBへ接続せず、実データ、認証情報、Bearer Tokenを使いません。
- DOCXへマクロ、VBA、外部画像、外部ハイパーリンク、`TargetMode="External"` のRelationshipを追加しません。
- `w:lang` は日本語を `ja-JP`、台灣華語を `zh-TW` とし、日本語はYu Gothic UI、台灣華語はMicrosoft JhengHeiを指定します。

## 検証

`generate_manual.ps1` は次を自動検証し、違反時は失敗します。

- DocumentFormat.OpenXml 3.5.1の `OpenXmlValidator` が0件であること
- Relationshipに外部Targetがないこと
- マクロ関連パーツがないこと
- 必須の4画像が存在すること

生成後はWordで開き、共通目次、各言語9章、警告欄、図の読みやすさ、長い障害対応表の改ページを目視確認してください。実フォームを変更した場合は、DraftとAwaitingUserの状態・ボタン・タブが最新実装と一致することも確認します。
