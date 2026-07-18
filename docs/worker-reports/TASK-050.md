# TASK-050 全ページ一括出力 実装報告

## Result summary

`REQ-EXPORT-001`を実装しました。画像書き出しボタンから「現在のページ／全ページ」「PNG／JPEG」「出力先フォルダー」「ファイル名の接頭辞」を選択でき、全ページは`ProjectDocument.Pages`の正規順で出力します。

## Git and environment

- Task: `TASK-050`
- Branch: `feature/TASK-050-batch-page-export`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-050`
- Functional base / start HEAD: `aa16cbc774b578ccd53b5101d919a44f546c63d8`
- Start status: clean（開始前に`git status --short`で確認）
- Start worktree: 主repositoryの`git worktree list --porcelain`に登録済み
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` => `6.0.428`
- Result commit: source implementation commit（完了時にledgerへfull hashを記録）

## Changed files

- `MojiCollaTool/MojiCollaTool/Export/PageExportService.cs`
  - 出力対象の計画、正規順、zero-padding、ファイル名sanitize、衝突検出、temporary fileからの確定移動、page単位失敗継続を担当。
- `MojiCollaTool/MojiCollaTool/Export/PageExportDialog.cs`
  - 日本語の小型WPFダイアログ。
- `MojiCollaTool/MojiCollaTool/MainWindow.xaml.cs`
  - active sessionだけを対象に、編集中ページを先にcaptureし、非active pageを既存のBindPage境界で切替・描画。完了後にactive page、選択object、zoomを復元。
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
  - view stateの取得・復元と、PNG透明背景／JPEG白背景のrender境界を追加。
- `tests/MojiCollaTool.Tests/TASK050ExportTests.cs`
  - 命名、衝突、失敗継続、開始失敗、PNG alpha、JPEG背景、論理寸法を検証。
- `CHANGELOG.md`、`docs/planning/implementation-ledger.md`、`docs/testing/verification-matrix.md`

## Adopted policies

- 命名: `接頭辞_{project order + 1のzero-padded index}_{sanitize済みpage名}.png|jpg`。2桁を最低幅とし、100ページ以上では必要桁数へ拡張します。無効文字、制御文字、末尾のdot/space、空名、Windows予約名を安全化し、日本語・Unicodeは保持します。ページ名自体は変更しません。
- 衝突: 開始前に全出力先を検査し、1件でも既存なら何も書かず明示エラーにします。描画は一時ファイルへ行い、成功時だけ確定名へ移動するため、既存ファイルを上書きしません。
- JPEG: alphaを持たないため、出力時だけ白い不透明背景を合成します。PNGはworkspaceの灰色背景を出力へ持ち込まず、CanvasColorと画像assetのalphaを保持します。
- 失敗継続: 出力先フォルダーを作成できない等、batch開始前の失敗は何も書かず中止します。開始後の1ページ失敗は残りを継続し、成功数・失敗数・失敗ページを日本語で表示します。
- dirty/state: render前にactive pageをcaptureし、page切替はsessionの既存境界を使用します。serviceはdocumentを変更せず、MainWindowは処理後にactive page、選択object、zoomを復元します。

## Verification

- Debug build: pass、0 warnings、0 errors
- Debug test: 106 passed、0 failed
- Release build: pass、0 warnings、0 errors
- Release test: 106 passed、0 failed
- `git diff --check`: pass
- GUIの実操作（dialogからのフォルダー参照、複数ページの目視確認、縦書き／横書きの目視）はこの実行では`Not verified`です。WPF/STAのrender integration、PNG/JPEGの寸法・背景、service単体の命名・衝突・失敗継続は自動検証済みです。

## Known issues and rollback

- GUI手動確認が未実施です。既存のcurrent-page export APIとsession/page asset境界を再利用しています。
- rollbackは本taskのresult commitをrevertし、既存の`OutputImageButton_Click`と`PageEditorControl.ExportImage`へ戻します。project XML/schema、依存package、balloon/undo内部は変更していません。
