# 実装台帳

計画commit時点では機能未実装。workerは自分の行だけを更新する。

| Task | Status | Branch | Worktree | Base commit | Result commit | Requirements | ADR | Build | Tests | Manual/UI/Compatibility | Review/Integration | Blocker | Next |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

## TASK-005 handoff update

| Task | Status | Branch | Worktree | Base | Requirements | Build | Tests | Compatibility / error handling | Next |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TASK-005 | Complete | `feature/TASK-005-safe-legacy-persistence` | `F:/github/MojiCollaTool-worktrees/TASK-005` | `939c457` | `REQ-SAVE-001`, `REQ-COMPAT-001`, `REQ-NFR-ERROR-001` | Debug/Release pass, 0 warnings, 0 errors | 21 passed | Temporary workspace, explicit Image1/Image2 copy, duplicate-extension rejection, decoder/dimension validation, stale removal, Japanese outer errors with detailed log preservation, safe replace, save/load failure preservation | TASK-040/041 |
| TASK-000 | Complete | feature/TASK-000-build-baseline | `F:/github/MojiCollaTool-worktrees/TASK-000` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | final handoffに記載 | BUILD,TEST,PERF | - | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 fixed | 8 passed; p95 89.387 ms / threshold 500 ms | Smoke起動確認、UI操作はNot verified、fixture互換性確認 | Locally verified; integration pending | なし | TASK-005/010はこの結果統合後に開始可 |
| TASK-005 | Planned | - | - | - | - | SAVE,COMPAT,ERROR | ADR-0005 | Not run | Not run | Not verified | - | TASK-000 | 000後 |
| TASK-010 | Complete | feature/TASK-010-project-page-model | `F:/github/MojiCollaTool-worktrees/TASK-010` | `939c4576884a05b0c265ab8cb42240f8dd1e88ae` | `ef8230a`（追加検証・修正）、報告書参照 | PAGE,WORKSPACE | ADR-0001 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | 16 passed; ProjectDocumentTests 8件 | UI変更なし、縦横MojiData、ProjectId/PageId保持、順序正規化、重複拒否を確認 | Pending integration review | なし | TASK-020/025/040/060 |
| TASK-020 | Complete | feature/TASK-020-page-editor-control | `F:/github/MojiCollaTool-worktrees/TASK-020` | `391a0d8` | 実装・修正・検証結果はTASK-020報告書参照 | PAGE,MEM | ADR-0002 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | Debug/Release 31 passed | UI起動/終了スモークpass、STAライフサイクル2件pass、視覚的タブ操作はT030範囲 | Pending review | なし | TASK-030/070/190B |
| TASK-025 | Complete | feature/TASK-025-workspace-project-session | `F:/github/MojiCollaTool-worktrees/TASK-025` | `a46c29a`（TASK-010 HEAD） | `68adb512da81a7d27eccaca57b7eb0b419f3648a` | WORKSPACE,DIRTY | ADR-0001 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | 24 passed; WorkspaceTests 8件 | UI/serializer変更なし。複数session隔離、active page、path重複拒否、dirty close、単調増加revision tokenを確認 | Pending integration review | なし | TASK-030 |
| TASK-030 | Planned | - | - | - | - | PAGE,WORKSPACE,DIRTY,UI | ADR-0001/2 | Not run | Not run | Not verified | - | 020,025,041 | dependency後 |
| TASK-040 | Complete | `feature/TASK-040-versioned-project-format` | `F:/github/MojiCollaTool-worktrees/TASK-040` | `18f01d1` | `HEAD` | SAVE,PAGE,OBJECT | ADR-0005 | Debug/Release pass, 0 warnings, 0 errors | 33 passed | Versioned manifest/page XML, Japanese path round-trip, unknown version rejection, traversal rejection, backup replacement | Pending integration review | None | TASK-041 |
| TASK-041 | Planned | - | - | - | - | COMPAT | ADR-0005 | Not run | Not run | Not verified | - | TASK-040 | 040後 |
| TASK-050 | Planned | - | - | - | - | EXPORT | - | Not run | Not run | Not verified | - | TASK-030 | 030後 |
| TASK-060 | Planned | - | - | - | - | OBJECT,ZORDER | ADR-0003 | Not run | Not run | Not verified | - | TASK-010 | 010後 |
| TASK-070 | Planned | - | - | - | - | UNDO,DIRTY,MEM | ADR-0004 | Not run | Not run | Not verified | - | 020,060 | dependency後 |
| TASK-080 | Planned | - | - | - | - | ZORDER,LOCK,UI | ADR-0003/4 | Not run | Not run | Not verified | - | TASK-070 | 070後 |
| TASK-090 | Planned | - | - | - | - | SYMBOL,UNICODE | ADR-0006 | Not run | Not run | Not verified | - | 060,070 | dependency後 |
| TASK-100 | Planned | - | - | - | - | SYMBOL,UI | ADR-0006 | Not run | Not run | Not verified | - | TASK-090 | 090後 |
| TASK-110 | Planned | - | - | - | - | BALLOON | ADR-0007 | Not run | Not run | Not verified | - | 060,070 | dependency後 |
| TASK-120 | Planned | - | - | - | - | BALLOON,PERF | ADR-0007 | Not run | Not run | Not verified | - | TASK-110 | 110後 |
| TASK-130 | Planned | - | - | - | - | BALLOON link | ADR-0007 | Not run | Not run | Not verified | - | TASK-120 | 120後 |
| TASK-140 | Planned | - | - | - | - | LAYOUT | ADR-0008 | Not run | Not run | Not verified | - | TASK-130 | 130後 |
| TASK-150/160 | Planned | - | - | - | - | BALLOON Could | ADR-0007 | Not run | Not run | Not verified | - | TASK-130 | future |
| TASK-170 | Planned | - | - | - | - | CLIPBOARD | ADR-0001/4 | Not run | Not run | Not verified | - | 030,070 | dependency後 |
| TASK-180 | Planned | - | - | - | - | BACKGROUND-FX | - | Not run | Not run | Not verified | - | TASK-140 | future |
| TASK-190A | Ready | feature/TASK-190A-fork-documentation | worktree-map参照 | plan base | - | FORK,DEPS | - | N/A: docs-only | N/A | Not verified | Not reviewed | なし | worker開始可 |
| TASK-190B | Planned | - | - | - | - | FORK,UI | - | Not run | Not run | Not verified | - | 020,190A | dependency後 |
| TASK-200 | Planned | - | - | - | - | UI | - | Not run | Not run | Not verified | - | TASK-190B | 190B後 |
| TASK-210/220/230 | Planned | - | - | - | - | OPS,SNAP,SELECT,GROUP | ADR-0003/4 | Not run | Not run | Not verified | - | task-breakdown参照 | future |
