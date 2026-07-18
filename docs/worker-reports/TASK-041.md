# TASK-041 作業報告

## Result

旧形式のroot-entry `.mctzip` を検出し、`ProjectDocument` の1ページへ移行して読み込む共通readerを実装した。現行形式はmanifest検出後に既存のversioned readerへ委譲し、ファイル拡張子には依存しない。

## Git

- Base commit: `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e`
- Result commit: `60d72a5685c2a365dc50994a85ea0f6a25af3a12`
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

## Rollback

`ProjectReader` の形式判定登録と `DataIO.ReadProject` を取り除けば、既存のversioned／legacy個別reader境界へ戻せる。
