# Worktree台帳

## 運用規則

- 本表のpathは `git worktree add`で作成し、`git worktree list --porcelain`への登録を確認する。
- directoryコピー、`.git`コピー、コピー先での`git init`はworktree作成として認めない。
- Git metadata権限や所有者エラーが発生した場合、workerは未管理コピーへ退避せず作業を停止する。
- 各taskの開始前にbranch、worktree、functional base commit、exact開始時HEAD、clean status、worker promptの存在を司令役とworkerの双方で確認する。
- 詳細checklistは司令役が各worktreeへGit管理外で配置する`AGENTS.md`と`docs/worker-prompts/README.md`を正とし、AI向け指示文書をtask commitへ含めない。

第1陣は共通base `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a`（tag `plan-base-20260717`）から作成し、branch、HEAD、clean状態、AGENTS、worker promptを検証済みである。所有者不一致対策は、ユーザー承認を得て主repositoryと下記2worktreeの正確なpathだけをglobal `safe.directory`へ登録した。

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-000 | `feature/TASK-000-build-baseline` | `F:/github/MojiCollaTool-worktrees/TASK-000` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | なし | `docs/worker-prompts/TASK-000.md` | Complete; `develop`へ統合済み |
| TASK-190A | `feature/TASK-190A-fork-documentation` | `F:/github/MojiCollaTool-worktrees/TASK-190A` | `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a` | なし | `docs/worker-prompts/TASK-190A.md` | Complete; `develop`の`88c7062`で統合済み |
| TASK-190B | `feature/TASK-190B-app-branding` | `F:/github/MojiCollaTool-worktrees/TASK-190B` | `1e64a715adeb2102f15b6a932075f5afdb65ff44` | TASK-020、TASK-190A | `docs/worker-prompts/TASK-190B.md` | Complete; `d727b8b`; integrated at `b80402e` |

## 統合済み

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-041 | `feature/TASK-041-legacy-project-import` | `F:/github/MojiCollaTool-worktrees/TASK-041` | `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e` | TASK-040まで`develop`へ統合済み | `docs/worker-prompts/TASK-041.md` | Complete; `develop`の`8875af4`までに統合済み |
| TASK-030 | `feature/TASK-030-project-page-tabs` | `F:/github/MojiCollaTool-worktrees/TASK-030` | `8875af4696663f34a6ba72f255b2f9e209578dd2` | TASK-020、TASK-025、TASK-041統合済み | `docs/worker-prompts/TASK-030.md` | Complete; `develop`の`7fc27d5`までに統合済み |

## TASK-070統合後wave

下記2taskは、本台帳更新を含む同一の`develop`開始commitから司令役が正式なworktreeを登録し、各worktreeへlocal-onlyのAGENTSとworker promptを配置する。

| Task | Branch | Worktree path | Functional base | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-050 | `feature/TASK-050-batch-page-export` | `F:/github/MojiCollaTool-worktrees/TASK-050` | `aa16cbc774b578ccd53b5101d919a44f546c63d8` | TASK-030、TASK-070統合済み | `docs/worker-prompts/TASK-050.md`（local-only） | Complete; integrated at `726ba07e2dcfff0fe4e43e929a24731de3c6059f` |
| TASK-110 | `feature/TASK-110-balloon-model` | `F:/github/MojiCollaTool-worktrees/TASK-110` | `aa16cbc774b578ccd53b5101d919a44f546c63d8` | TASK-060、TASK-070統合済み | `docs/worker-prompts/TASK-110.md`（local-only） | Complete; integrated at `fd307beec9d0cf6c8865f219a99ca44ce2353733` |
