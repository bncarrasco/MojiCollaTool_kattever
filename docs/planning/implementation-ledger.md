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
| TASK-010 | Complete | feature/TASK-010-project-page-model | `F:/github/MojiCollaTool-worktrees/TASK-010` | `939c4576884a05b0c265ab8cb42240f8dd1e88ae` | `ef8230a`（追加検証・修正）、報告書参照 | PAGE,WORKSPACE | ADR-0001 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | 16 passed; ProjectDocumentTests 8件 | UI変更なし、縦横MojiData、ProjectId/PageId保持、順序正規化、重複拒否を確認 | Integrated to `develop` at `391a0d8` | なし | TASK-020/025/040/060 |
| TASK-020 | Complete | feature/TASK-020-page-editor-control | `F:/github/MojiCollaTool-worktrees/TASK-020` | `391a0d8` | `6fb4094`, `4fb0475` | PAGE,MEM | ADR-0002 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | Debug/Release 31 passed | UI起動/終了スモークpass、STAライフサイクル2件pass、視覚的タブ操作はT030範囲 | Integrated to `develop` at `ae7faff` | なし | TASK-030/070/190B |
| TASK-025 | Complete | feature/TASK-025-workspace-project-session | `F:/github/MojiCollaTool-worktrees/TASK-025` | `a46c29a`（TASK-010 HEAD） | `68adb512da81a7d27eccaca57b7eb0b419f3648a` | WORKSPACE,DIRTY | ADR-0001 | Debug/Release pass (0 warnings, 0 errors), SDK 6.0.428 | 24 passed; WorkspaceTests 8件 | UI/serializer変更なし。複数session隔離、active page、path重複拒否、dirty close、単調増加revision tokenを確認 | Integrated to `develop` at `3daaebd` | なし | TASK-030 |
| TASK-030 | Complete | `feature/TASK-030-project-page-tabs` | `F:/github/MojiCollaTool-worktrees/TASK-030` | `8875af4696663f34a6ba72f255b2f9e209578dd2` | `d7ec6847d33c06a118f6339425566ee4843bd6d9` | REQ-PAGE-001, REQ-WORKSPACE-001, REQ-DIRTY-001, REQ-UI-JA-001 | ADR-0001/2 | Debug/Release pass, 0 warnings, 0 errors | 71 passed | capture/変更通知分離、STA tab選択復元、画像transaction失敗保護、二段tab、session/page asset隔離、UI/manualはNot verified | Integrated to `develop` at `7fc27d5`; commander reverified 71/71 | なし | TASK-050/TASK-170 |
| TASK-040 | Complete | `feature/TASK-040-versioned-project-format` | `F:/github/MojiCollaTool-worktrees/TASK-040` | `18f01d1` | `a592464`, `40b1f0b`, `95426e8` | SAVE,PAGE,OBJECT | ADR-0005 | Debug/Release pass, 0 warnings, 0 errors | 42 passed | Image metadata/asset one-to-one and decode/dimension validation, sinkless rejection, batch restore, 0/1/2 image round-trip, order/name/DTD/traversal/fault validation | Integrated to `develop` at `c50a81e` | None | TASK-041 |
| TASK-041 | Complete | `feature/TASK-041-legacy-project-import` | `F:/github/MojiCollaTool-worktrees/TASK-041` | `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e` | `60d72a5`, `27cea4d` | COMPAT | ADR-0005 | Debug/Release pass, 0 warnings, 0 errors | 60 passed | Legacy detector/reader/mapper、画像0/1/2枚、非batch/batch復元、未知field、壊れたmanifest非fallback、entry上限、日本語path | Integrated to `develop` at `8875af4`; commander reverified 60/60 | None | TASK-030 |
| TASK-050 | Dependency ready | `feature/TASK-050-batch-page-export` | 未作成 | `7fc27d5`（T030統合HEAD） | - | EXPORT | - | Not run | Not run | Not verified | Worker prompt/worktree準備待ち。T190BとMainWindow競合 | なし | TASK-060との並行候補 |
| TASK-060 | Complete (local) | `feature/TASK-060-object-id-zindex` | `F:/github/MojiCollaTool-worktrees/TASK-060` | `1e64a715adeb2102f15b6a932075f5afdb65ff44` | `6ff780d`（96b89e6の修正） | OBJECT,ZORDER | ADR-0003 | Not verified: .NET SDK 6.0.428 missing（dotnet test exit 1） | Not verified: SDK解決でtest project起動不可 | UI変更なし。UUID/Type/ZIndex/lock/visibility/relationship fields、page/project duplicate checks、canonical Z snapshot、複数object clone/legacy XML確認を追加。TemporaryDirectory参照を修正し、旧int ID保持を修正 | Pending integration review | SDK 6.0.428未導入、専用TASK-060.md未配置 | TASK-070/080 |
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
| TASK-190A | Complete | `feature/TASK-190A-fork-documentation` | `F:/github/MojiCollaTool-worktrees/TASK-190A` | `adf5fd8` | `d297bb6`, `7f2679f`, `2682a0b` | FORK,DEPS | - | N/A: docs-only | N/A: docs-only | README/notice review済み。配布時対応はT190B | Integrated to `develop` at `88c7062` | なし | TASK-190B |
| TASK-190B | Dependency ready | `feature/TASK-190B-app-branding` | 未作成 | `7fc27d5`（現develop） | - | FORK,UI | - | Not run | Not run | Not verified | Worker prompt/worktree準備待ち。T050とMainWindow/XAML競合 | なし | TASK-060との並行候補、T050とは直列 |
| TASK-200 | Planned | - | - | - | - | UI | - | Not run | Not run | Not verified | - | TASK-190B | 190B後 |
| TASK-210/220/230 | Planned | - | - | - | - | OPS,SNAP,SELECT,GROUP | ADR-0003/4 | Not run | Not run | Not verified | - | task-breakdown参照 | future |
