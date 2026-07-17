# TASK-025 作業報告

## Result

`ApplicationWorkspace` と `ProjectSession` を追加し、複数projectのdocument、FilePath、active page、dirty/revision stateをUIから分離して管理できるようにした。revisionは過去へUndoして別編集しても既存tokenを再利用しない。serializer、MainWindow、既存project形式は変更していない。

## Summary

- `ApplicationWorkspace` が複数session、active project/page、session追加・切替・closeを管理する。
- FilePathは絶対pathへ正規化し、大小文字を無視して同一fileの二重openとsave-as衝突を拒否する。
- `ProjectSession` はProjectDocument、FilePath、CurrentRevision、SavedRevision、IsDirty、ActivePageIdを所有する。
- `MarkChanged`、`MarkSaved`、`RestoreRevision` により、後続のUndo/Redo serviceがsaved revisionへ戻れるshell-facing markerを提供する。
- `_nextRevision`を別管理し、`RestoreRevision`でcurrentを過去へ戻しても発行済み最大値を巻き戻さない単調増加tokenにした。
- dirty sessionのcloseは既定で拒否し、`DiscardChanges` または `AllowDirty` policyを明示した場合だけ解放する。
- `INotifyPropertyChanged` とsession lifecycle eventを公開し、後続UIが状態を購読できる境界を作った。

## Scope / requirements

- 要件: `REQ-WORKSPACE-001`, `REQ-DIRTY-001`
- ADR: `ADR-0001`
- Scope: session collection、active project/page、FilePath、重複path、close policy、revision marker
- Out: visual tabs、serializer変更、MainWindow変更、Undo/Redo command本体

## Changed files

- `MojiCollaTool/MojiCollaTool/Workspace/ApplicationWorkspace.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/ProjectSession.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/ProjectSessionEventArgs.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/CloseSessionPolicy.cs`
- `tests/MojiCollaTool.Tests/WorkspaceTests.cs`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`

## Branch / worktree

- Branch: `feature/TASK-025-workspace-project-session`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-025`
- Base: `a46c29a`（TASK-010 HEAD）
- Result commit: final amended T025 commit（`git log`参照）
- 正式Git worktreeとして作成し、実装・テスト・報告書・台帳をこのbranchへ集約する。

## Verification

| Command | Result |
| --- | --- |
| `powershell -ExecutionPolicy Bypass -File eng/test.ps1` | Pass: 24 passed, 0 failed, 0 skipped |
| Debug build | Pass: 0 warnings, 0 errors |
| Release build | Pass: 0 warnings, 0 errors |
| performance fixture | Pass: p95 74.808 ms / threshold 500 ms |

WorkspaceTestsでは、2つのprojectのpage/dirty状態隔離、active page切替、正規化pathの重複拒否、dirty close policy、session解放、saved revision復元、保存→Undo→別編集のdirty維持、saved revisionへ戻った場合だけcleanになること、revision token衝突防止を検証した。

## Known risks / downstream impact

- documentの直接mutationは自動検知せず、変更経路は`ProjectSession.Execute`または`MarkChanged`を使う必要がある。TASK-070でcommand/history境界へ統合する。
- 実ファイルの読み書き・保存成功時の`MarkSaved`呼出しはserializer taskが担当する。
- visual tabとdirty indicatorの表示はTASK-030で接続する。

## Rollback

新規Workspace配下の4実装ファイル、WorkspaceTests、台帳・検証行・本報告を削除すればT025分だけを戻せる。既存のT010 document modelとserializerは変更していない。
