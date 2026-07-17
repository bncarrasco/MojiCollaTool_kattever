# TASK-040 実装報告

## 結果

完了。`manifest.xml`、ページ単位XML、ページごとの背景画像実体を持つバージョン化複数ページ形式を実装しました。

## 概要

- `IProjectReader` / `IProjectWriter` と `VersionedProjectReader` / `VersionedProjectWriter` を追加。
- `manifest.xml`、`pages/{PageId}/page.xml`、`image1` / `image2` のZip形式を追加。
- グローバルなWorkingディレクトリに依存しない `IProjectAssetSource` / `IProjectAssetSink` を追加。
- CanvasのImageDataと画像entryの1対1整合性、実画像のdecode、OriginalWidth/OriginalHeight一致を保存・読込の双方で検証。
- 画像entry付きのsinkなし読込を拒否し、writer自己検証は画像を破棄する通常readerから分離。
- 複数画像の復元は一括sinkを利用し、復元失敗時の部分反映を防止。
- `FormatVersion` と `MinimumReaderVersion` の検証、ProjectId/PageId/order の整合性検証を追加。
- XML DTD 無効化、Zipの件数・サイズ上限、絶対パス・`..` traversal拒否を追加。
- manifestとpage.xmlのページ名一致検証、ページ順序0..N-1の連続値検証を追加。
- 一時 Zip の閉鎖後検証、原子的な置換、任意の `.backup` 保存を実装。
- `DataIO.ReadVersionedProject` / `WriteVersionedProject` をアダプターとして追加。既存の旧形式UI経路は変更していません。

## 対応要件・設計

- REQ-SAVE-001, REQ-PAGE-001, REQ-OBJECT-001
- ADR-0005

## ブランチ・worktree・基点

- ブランチ: `feature/TASK-040-versioned-project-format`
- worktree: `F:/github/MojiCollaTool-worktrees/TASK-040`
- 基点: `18f01d1` (`feature/TASK-005-safe-legacy-persistence`)
- 結果コミット: `HEAD`（ページ背景画像実体の保存）

## 変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `MojiCollaTool/MojiCollaTool/DataIO.cs`
- `tests/MojiCollaTool.Tests/VersionedProjectPersistenceTests.cs`
- `docs/planning/implementation-ledger.md`
- `CHANGELOG.md`

## 検証

| コマンド | 結果 |
| --- | --- |
| `powershell.exe -ExecutionPolicy Bypass -File eng\test.ps1` | 成功: 42件成功、0件失敗 |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Debug` | 成功: 警告0件、エラー0件 |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Release` | 成功: 警告0件、エラー0件 |
| `git diff --check` | 成功 |

実PNG/JPEGのdecodeと寸法一致、日本語のプロジェクト名・ページ名・本文とページごとの背景画像（0枚・1枚・2枚）の往復、日本語パス、metadata/asset不一致、破損画像、画像の不要entry不在、sinkなし読込、復元失敗時の部分反映防止、未知のmajor版、将来のminimum reader、DTD、Zipパス逸脱、ページ順序不連続、ページ名不一致、画像source失敗時の原本保護、1世代バックアップ置換を検証しました。

## 既知のリスク・後続作業

- 画像の取得元・復元先はasset source/sinkの呼び出し側がProjectSession単位で実装します。
- 複数画像を扱うsinkは `IProjectAssetBatchSink` を実装し、全件成功時だけProjectSessionへ反映する必要があります。
- TASK-041では共通reader境界を利用してlegacy形式の検出・移行を追加できます。
