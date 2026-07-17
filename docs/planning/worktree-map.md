# Worktree台帳

計画baseの正確なhashとpathはworktree作成後に検証し、司令役最終報告を正式な実行記録とする。Git commitは自己hashを本文に埋め込めないため、指示書では `plan-base-20260717` tagと各worktreeの `git rev-parse HEAD`を使用する。

| Task | Branch | Worktree path | Base commit | Dependencies | Worker prompt | Status |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-000 | `feature/TASK-000-build-baseline` | `F:/github/MojiCollaTool-worktrees/TASK-000` | `plan-base-20260717` | なし | `docs/worker-prompts/TASK-000.md` | Planned; SDK blocker |
| TASK-190A | `feature/TASK-190A-fork-documentation` | `F:/github/MojiCollaTool-worktrees/TASK-190A` | `plan-base-20260717` | なし | `docs/worker-prompts/TASK-190A.md` | Planned |
