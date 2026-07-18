# TASK-070 Undo／Redo・dirty基盤 実装報告

## Result

### Final handoff record

- Implementation commit: `4c71e43e5aa70881f5fae38272ba9e9ed4cbda01`
- Current handoff commit before this follow-up: `9360249bb74b6be1876686b4b7ed1ace93930f02`
- Final implementation commit: `0056d65757cfa7969258cf18c4e9076c6967a5e4`
- This follow-up adds active-page restoration, saved-path-safe trim, and the final regression cases.
- Validation: Debug 101/101 passed; Release 101/101 passed; 0 failed.

## Summary

- `ProjectSession`ごとに`UndoRedoHistory`を所有し、複数projectの履歴・dirty・active pageを隔離。
- semantic operationとpage mementoを使い、ページ内容は影響ページだけdeep copy、ページ順序はID列で保持。
- 画像は`ProjectAssetRestore`のbefore/afterを履歴entryへ保持し、Undo/Redo時に`SaveImages`で一括復元。
- transaction、500ms以内の同一ページ・同一操作のcoalesce、redo branch破棄、件数/推定byte上限trimを追加。
- `Ctrl+Z`、`Ctrl+Y`、`Ctrl+Shift+Z`をWPF command routingへ接続。TextBox側の標準command処理を優先するrouteとした。

## 指摘対応

- coalesce時は履歴nodeのrevisionを維持し、sessionのCurrentRevisionを旧値へ戻す。保存後の次編集は新しいrevisionを払い出す。
- page dirtyは保存nodeとの共通ancestor自体を除外して差分pathだけを集計する。
- Undo/Redoのasset復元callbackが失敗した場合はdocumentを反対側へ戻し、stack/current nodeを変更しない。
- active pageのbefore/after IDをentryに保持し、page削除のUndo/Redoで選択ページも復元する。
- trim時は保持entryの境界parentをrootへ付け替え、古いHistoryNode chainを切断する。
- UIイベントへ文字入力・スタイル変更・位置変更・キャンバス変更・画像変更などのoperation名とobject単位coalesce keyを付与した。

## 対応要件

`REQ-UNDO-001`、`REQ-DIRTY-001`、`REQ-NFR-MEM-001`。

## 開始時Git確認

- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-070`
- Branch: `feature/TASK-070-undo-dirty`
- Base/開始HEAD: `3f212391be8b2e4433617e8f67a97b00f161cf7f`
- 開始時status: clean
- SDK: `6.0.428`

## 設計判断

- 履歴entryはproject全体snapshotではなく、affected pageのmementoと全page ID順序を持つ。追加/削除ページはbefore/afterの差分IDを自動的にaffectedへ追加する。
- memory costはmementoのページ/オブジェクトcollection推定値、ページ順序、保持asset byte、asset metadataを合算する。初期上限は100 entry／64 MiB。
- trimは古いundo entryまたはredo entryをlistから除去し、entryが保持するpage cloneとasset byteへの参照を切断する。redo branchは新規編集時に全削除する。
- 画像操作はdocument変更と同じhistory entryのasset before/afterを一括反映し、page削除・複製・画像置換で不整合期間を作らない。
- coalesce境界は同一description・同一affected page・500ms以内。transactionは明示的な`BeginTransaction`からdisposeまでを1件とする。Undo/Redo、save marker、対象変更はwindowをリセットする。
- shortcutは`ApplicationCommands`のcommand routingを使用し、標準TextBoxのlocal Undoが先に処理されるfocus優先順位を維持する。

## 旧T070成果の採否

archive branchの実装はmerge/cherry-pickせず、履歴APIとテスト観点だけ参考にした。現行baseへpage memento、asset transaction、branch-safe dirtyを再実装した。

## OQ-UNDO-002提案

初期値は100 entry／64 MiB。実測値はページmementoの推定collection overheadとasset byteを加算し、いずれかを超えた時点で最古entryを切断する。代表画像を含む長時間計測は統合後に追加する。

## 検証

- Debug build: pass、0 warnings/0 errors。
- Debug tests: 94 passed、0 failed。
- Release build: pass、0 warnings/0 errors。
- Release tests: 94 passed、0 failed。
- `git diff --check`: pass。
- 自動テスト: page追加/削除/復元、名前変更、dirty saved revision、transaction、coalesce、redo branch、asset複製復元、asset rollback、active page復元、共通node除外、trim chain切断、operation key分離、既存回帰。
- Manual/UI: 縦書き/横書き入力、drag、複数project切替、About画面はNot verified。

## 既知の問題・後続影響

- direct mutationを行う既存MojiWindowの変更通知は現行capture境界に依存する。drag中のpreviewはUIへ逐次反映するが、履歴commitはmouse upの通知時に行う。
- 実際のUI操作によるfocus優先順位と画像置換の視覚確認は統合環境で再確認する。
- TASK-080以降は各object/page commandを`ProjectSession.Execute`またはtransaction境界へ接続する。

## マージ注意・rollback

- MainWindow、ProjectSession、ProjectDocumentに変更があるため、後続MainWindow作業との競合を確認する。
- rollbackはこのworktreeのResult commitをrevertし、既存のversioned/legacy persistence変更は保持する。
