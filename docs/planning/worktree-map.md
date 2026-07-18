# Worktree台帳

## 運用規則

- 本表のpathは `git worktree add`で作成し、`git worktree list --porcelain`への登録を確認する。
- directoryコピー、`.git`コピー、コピー先での`git init`はworktree作成として認めない。
- Git metadata権限や所有者エラーが発生した場合、workerは未管理コピーへ退避せず作業を停止する。
- 各taskの開始前にbranch、worktree、functional base commit、exact開始時HEAD、clean status、worker promptの存在を司令役とworkerの双方で確認する。
- 詳細checklistは `AGENTS.md` と `docs/worker-prompts/README.md`を正とする。

第1陣は共通base `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a`（tag `plan-base-20260717`）から作成し、branch、HEAD、clean状態、AGENTS、worker promptを検証済みである。所有者不一致対策は、ユーザー承認を得て主repositoryと下記2worktreeの正確なpathだけをglobal `safe.directory`へ登録した。

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-000 | `feature/TASK-000-build-baseline` | `F:/github/MojiCollaTool-worktrees/TASK-000` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | なし | `docs/worker-prompts/TASK-000.md` | Ready; SDK blocker |
| TASK-190A | `feature/TASK-190A-fork-documentation` | `F:/github/MojiCollaTool-worktrees/TASK-190A` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | なし | `docs/worker-prompts/TASK-190A.md` | Ready |

## 統合済み

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-041 | `feature/TASK-041-legacy-project-import` | `F:/github/MojiCollaTool-worktrees/TASK-041` | `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e` | TASK-040まで`develop`へ統合済み | `docs/worker-prompts/TASK-041.md` | Complete; `develop`の`8875af4`までに統合済み |

## 次作業

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-030 | `feature/TASK-030-project-page-tabs` | `F:/github/MojiCollaTool-worktrees/TASK-030` | `8875af4696663f34a6ba72f255b2f9e209578dd2` | TASK-020、TASK-025、TASK-041統合済み | `docs/worker-prompts/TASK-030.md` | Ready; 司令役が正式worktree登録とexact開始時HEADをkickoffで確認する |
