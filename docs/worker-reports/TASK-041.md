# TASK-041 作業報告

## Result

旧形式のroot-entry `.mctzip` を検出し、`ProjectDocument` の1ページへ移行して読み込む共通readerを実装した。現行形式はmanifest検出後に既存のversioned readerへ委譲し、ファイル拡張子には依存しない。

## Git

- Base commit: `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e`
- Result commit: `27cea4deac8447ecb771f111bdbe52319dba3880`
- Branch: `feature/TASK-041-legacy-project-import`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-041`

## 対応要件

- REQ-COMPAT-001
- ADR-0005

## 変更内容

- `ProjectFormatDetector` と `ProjectReader` を追加し、entry件数上限をentry名の配列化前に適用。
- `LegacyProjectReader` を追加し、既存の安全なstaging/XML/image検証結果を `LegacyProjectDataAdapter` 経由で1ページへdeep-copy移行。
- 旧形式の画像をasset sinkへ復元。複数画像はbatch sinkを要求し、検証失敗時に既存データへ反映しない。
- `ProjectReadResult` と `ProjectReadWarning` を追加し、旧形式読込時に日本語の移行warningを返す。
- `DataIO.ReadProject` を追加。既存の旧形式UI経路とwriterは変更していない。
- 共通XML readerのDTD処理を禁止し、legacy XMLのDTD混入を拒否。

## 受入テスト

- 背景画像0枚、1枚＋非batch sink、2枚＋batch sink。
- 3件のMojiData、縦書き・横書き・Unicode値の保持。
- CanvasData/MojiDataの未知XML field無視。
- 壊れたmanifest.xmlとlegacy rootの共存時にlegacyへfallbackしないこと。
- entry上限超過を形式判定段階で拒否。
- 日本語pathとUnicodeオブジェクト。

## 検証

| コマンド | 結果 |
| --- | --- |
| `powershell.exe -ExecutionPolicy Bypass -File eng\\test.ps1` | 成功: 60 tests passed |
| `powershell.exe -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Debug` | 成功: 警告0、エラー0 |
| `powershell.exe -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Release` | 成功: 警告0、エラー0 |

## 未実施

- MainWindowへのwarning表示・新形式保存への自動移行は、仕様のScope外（MainWindow直接変更を避ける）として未実施。
- 実アプリの手動UI確認は未実施。

## レビュー追補

### 開始時Git確認

- 開始時の親リポジトリは `F:/github/MojiCollaTool_kattever`、`develop` は `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e`。
- 開始時点で `feature/TASK-041-legacy-project-import` は未作成、`TASK-041` は `.git` のないコピーだった。
- 旧コピーは `TASK-041-legacy-copy` へ退避し、正式worktree作成後にT041固有差分だけを移した。

### 正式worktree登録確認と逸脱事項

- `git worktree add -b feature/TASK-041-legacy-project-import F:/github/MojiCollaTool-worktrees/TASK-041 6cc9b43` を実行し、`git worktree list` で正式登録を確認した。
- 指示された開始点 `1c4420a` ではなく、functional base `6cc9b43` から開始してしまった。`1c4420a` は `6cc9b43` の子孫であり、機能差ではなくworktree運用文書を含めなかった手順逸脱である。履歴は変更せず、司令役が `develop` へtask固有commitを統合する。

### 変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/ProjectReader.cs`
- `MojiCollaTool/MojiCollaTool/DataIO.cs`
- `tests/MojiCollaTool.Tests/LegacyProjectReaderTests.cs`
- `CHANGELOG.md`
- `docs/planning/implementation-ledger.md`
- `docs/worker-reports/TASK-041.md`

### 既知riskと後続影響

- MainWindowへのwarning表示・新形式保存への自動移行は未実施で、呼び出し側が `ProjectReadResult` とasset sinkを接続する必要がある。
- 画像復元の原子性はbatch sink実装側の契約に依存する。reader入口では全entry/XML/imageを先に検証する。
- TASK-030は本readerの結果をworkspace/page sessionへ接続する後続であり、legacyのwarning、page identity、asset sinkの接続を統合時に確認する。
- マージ時は `develop` の統合済みT020/T025/T040を基点として、`60d72a5685c2a365dc50994a85ea0f6a25af3a12` とレビュー修正commitのみを取り込む。rebase/mergeは行っていない。

### 手動・互換性確認状況

- 自動互換性確認: legacy root、新manifest形式、壊れたmanifest非fallback、日本語path、未知XML field、entry上限を確認済み。
- 縦書き・横書き: 3件のMojiDataで `Tategaki` / `Yokogaki` の値保持を自動確認済み。
- 日本語・Unicode: 日本語path、Unicode文字列、絵文字を自動確認済み。
- 手動確認: 実アプリUIの手動確認は未実施。
- 日本語UI: warningの文言は実装済みだが、画面表示の手動確認は未実施。

## Rollback

`ProjectReader` の形式判定登録と `DataIO.ReadProject` を取り除けば、既存のversioned／legacy個別reader境界へ戻せる。
