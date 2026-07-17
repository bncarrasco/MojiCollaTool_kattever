# TASK-040 実装報告

## Result

Complete。manifest とページ単位 XML を持つバージョン化複数ページ形式を実装しました。

## Summary

- `IProjectReader` / `IProjectWriter` と `VersionedProjectReader` / `VersionedProjectWriter` を追加。
- `manifest.xml` と `pages/{PageId}/page.xml` の Zip 形式を追加。
- `FormatVersion` と `MinimumReaderVersion` の検証、ProjectId/PageId/order の整合性検証を追加。
- XML DTD 無効化、Zip の件数・サイズ上限、絶対パス・`..` traversal 拒否を追加。
- 一時 Zip の閉鎖後検証、原子的な置換、任意の `.backup` 保存を実装。
- `DataIO.ReadVersionedProject` / `WriteVersionedProject` を adapter として追加。既存のlegacy UI経路は変更していません。

## Requirements / ADR

- REQ-SAVE-001, REQ-PAGE-001, REQ-OBJECT-001
- ADR-0005

## Branch / worktree / base

- Branch: `feature/TASK-040-versioned-project-format`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-040`
- Base: `18f01d1` (`feature/TASK-005-safe-legacy-persistence`)
- Result commit: `HEAD` (`Implement versioned multi-page project format`)

## Changed files

- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `MojiCollaTool/MojiCollaTool/DataIO.cs`
- `tests/MojiCollaTool.Tests/VersionedProjectPersistenceTests.cs`
- `docs/planning/implementation-ledger.md`
- `CHANGELOG.md`

## Verification

| Command | Result |
| --- | --- |
| `powershell.exe -ExecutionPolicy Bypass -File eng\test.ps1` | Pass: 33 passed, 0 failed |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Debug` | Pass: 0 warnings, 0 errors |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Release` | Pass: 0 warnings, 0 errors |
| `git diff --check` | Pass |

Tests cover two-page round-trip with Japanese project/page/text data, unknown major version rejection, Zip traversal rejection, Japanese filesystem paths, and one-generation backup replacement.

## Known risks / follow-up

- Image asset copying from the shared Working directory remains outside this model-only format layer and should be integrated by the later application/session task if required.
- TASK-041 can add legacy detection/migration through the common reader boundary.
