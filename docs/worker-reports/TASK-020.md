# TASK-020 作業報告

## Result

PageEditorControlへのページ内編集機能の抽出と、ページライフサイクル時のWindow所有解放を実装しました。

## 追加修正

- 一時的な`Unloaded`ではMojiWindowを閉じず、ページ削除・アプリ終了・`Dispose()`時だけ明示的に破棄
- Closed済みMojiWindowを`ShowMojiWindow()`で再生成可能に変更
- PageEditorControlに`IDisposable`と明示的な破棄処理を追加
- CanvasEditWindowの`Closed`でPageEditorControlへの相互参照を解除
- STAライフサイクルテストを追加

## Worktree / integration

- Branch: `feature/TASK-020-page-editor-control`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-020`
- develop T005 integration: `8a1b64f`
- develop T010 integration: `391a0d8`

## Verification

- SDK: .NET 6.0.428 (`C:/Users/user/.dotnet`)
- Debug build: pass, 0 warnings / 0 errors
- Release build: pass, 0 warnings / 0 errors
- Debug tests: 31 passed, 0 failed, 0 skipped
- Release tests: 31 passed, 0 failed, 0 skipped
- `PageEditorLifecycleTests`: 2 passed（Unloaded保持・MojiWindow再生成・Dispose解放・CanvasEditWindow参照解除）
- `git diff HEAD^ --check`: pass（追加ファイルをGit管理下へ追加済み）
- UI起動/終了スモーク: pass（WPFプロセスとWindow生成を確認）
- ページタブの視覚操作: T030の範囲のため未実装。T020では`Unloaded`相当のSTAライフサイクルを確認
