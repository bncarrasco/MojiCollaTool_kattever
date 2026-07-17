# TASK-005 実装報告

## Result

**Complete** — 旧形式 `.mctzip` の安全な保存・読込、画像entry検証、エラー表示改善を実装しました。

## Summary

- XML・文字データは共有Workingを入力にせず、空の保存用一時領域へ現在のモデルから全量生成する。
- 画像entryだけは現在のWorkingから `Image1.*` / `Image2.*` を明示的に取得して保存用一時領域へコピーする。
- 同一番号の複数拡張子、CanvasDataとの不整合、画像の復号失敗・寸法不一致は、zip置換またはWorking/UI反映前に拒否する。
- 画像削除後はstale imageを保存せず、保存・読込失敗時は既存archive/Workingを保持する。
- MainWindowの保存・読込・Working反映エラーは日本語の外側メッセージを表示し、内部例外の詳細はログだけに残す。

## Requirements / ADR

- `REQ-SAVE-001`, `REQ-COMPAT-001`, `REQ-NFR-ERROR-001`
- `ADR-0005-versioned-project-format-and-legacy-import.md`
- 新manifest/versioned multi-page formatはTASK-040、legacy mappingはTASK-041の対象。

## Branch / worktree / base

- Branch: `feature/TASK-005-safe-legacy-persistence`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-005`
- Base: `939c457` (`feature/TASK-000-build-baseline`)
- Result commit: handoff前に確定

## Changed files

- `MojiCollaTool/MojiCollaTool/DataIO.cs`
- `MojiCollaTool/MojiCollaTool/MainWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/AssemblyInfo.cs`
- `tests/MojiCollaTool.Tests/LegacyPersistenceTests.cs`
- `tests/MojiCollaTool.Tests/PersistenceErrorMessageTests.cs`
- `CHANGELOG.md`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`

## Verification

| Command | Result |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\test.ps1` | Pass: 21 passed, 0 failed; performance p95 8.232 ms / threshold 500 ms |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Debug` | Pass: 0 warnings, 0 errors |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Release` | Pass: 0 warnings, 0 errors |
| `git diff --check` | Pass |

Tests cover Japanese outer messages for save/load/Working commit, hiding internal dialog details while retaining exception text for logs, image round-trip, stale removal, corrupt image preservation, duplicate extensions, corrupt zip, traversal, and save failure preservation.

Manual UI save/load verification remains **Not verified**.

## Known risks / follow-up

- This task hardens the legacy root format; manifest/version gate and multi-page format remain for TASK-040/041.
- Rollback is to revert the TASK-005 implementation commit.
