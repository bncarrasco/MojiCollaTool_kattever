# 実装台帳

計画commit時点では機能未実装。workerは自分の行だけを更新する。

| Task | Status | Branch | Worktree | Base commit | Result commit | Requirements | ADR | Build | Tests | Manual/UI/Compatibility | Review/Integration | Blocker | Next |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TASK-000 | Integrated | feature/TASK-000-build-baseline | `F:/github/MojiCollaTool-worktrees/TASK-000` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | `939c4576884a05b0c265ab8cb42240f8dd1e88ae` | BUILD,TEST,PERF | - | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 fixed | 8 passed; p95 89.387 ms / threshold 500 ms | Smoke起動確認、UI操作はNot verified、fixture互換性確認 | Reviewed; integrated to develop | なし | TASK-005/010開始可 |
| TASK-005 | Planned | - | - | - | - | SAVE,COMPAT,ERROR | ADR-0005 | Not run | Not run | Not verified | - | TASK-000 | 000後 |
| TASK-010 | Planned | - | - | - | - | PAGE,WORKSPACE | ADR-0001 | Not run | Not run | Not verified | - | TASK-000 | 000後 |
| TASK-020 | Planned | - | - | - | - | PAGE,MEM | ADR-0002 | Not run | Not run | Not verified | - | TASK-010 | 010後 |
| TASK-025 | Planned | - | - | - | - | WORKSPACE,DIRTY | ADR-0001 | Not run | Not run | Not verified | - | TASK-010 | 010後 |
| TASK-030 | Planned | - | - | - | - | PAGE,WORKSPACE,DIRTY,UI | ADR-0001/2 | Not run | Not run | Not verified | - | 020,025,041 | dependency後 |
| TASK-040 | Planned | - | - | - | - | SAVE,PAGE,OBJECT | ADR-0005 | Not run | Not run | Not verified | - | 005,010 | dependency後 |
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
| TASK-190A | Ready | feature/TASK-190A-fork-documentation | worktree-map参照 | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | - | FORK,DEPS | - | N/A: docs-only | N/A | Not verified | Not reviewed | なし | worker開始可 |
| TASK-190B | Planned | - | - | - | - | FORK,UI | - | Not run | Not run | Not verified | - | 020,190A | dependency後 |
| TASK-200 | Planned | - | - | - | - | UI | - | Not run | Not run | Not verified | - | TASK-190B | 190B後 |
| TASK-210/220/230 | Planned | - | - | - | - | OPS,SNAP,SELECT,GROUP | ADR-0003/4 | Not run | Not run | Not verified | - | task-breakdown参照 | future |
