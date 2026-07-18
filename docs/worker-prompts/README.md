# ワーカー指示書の共通規則

各ワーカーは `AGENTS.md` と本書を読み、さらに自分の `TASK-XXX.md` に記載された作業場所だけを使用する。

## 指示書の必須項目

各 `TASK-XXX.md` は、少なくとも次を明記する。

- Task IDと担当範囲
- branch名
- worktreeの絶対path
- 依存機能を固定するfunctional base commit
- exact開始時HEAD。指示書自身を追加したcommitとの自己参照を避けるため、司令役のkickoff指示で提示してもよい
- 統合済みであるべき依存タスク
- 変更可能／変更禁止領域
- 受入条件と検証command
- 報告書、台帳、commitの要件

branch、worktree、functional base、開始時HEADのいずれかが未指定なら作業を開始しない。

## 開始前Git確認

編集前に次を実行する。

```powershell
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
git status --short
git worktree list --porcelain
```

確認条件:

- top-levelが指示されたworktree pathと一致する。
- branchが指示されたbranch名と一致する。
- HEADが指示された開始時HEADと一致する。開始時HEADはfunctional baseの子孫で、functional base以後の許可差分もkickoff指示と一致する。
- 作業開始時のstatusがcleanである。
- 主repositoryのworktree一覧に現在pathとbranchが登録されている。

## 禁止する代替手段

- repositoryや別worktreeを `Copy-Item`、`robocopy`、Explorer、`cp`等で複製して作業場所にする。
- `.git` directoryまたはlinked worktreeの `.git` fileをコピーする。
- コピーしたdirectoryで `git init`して正式worktreeの代用にする。
- Git権限不足を理由に未管理directoryで実装を続行する。
- 古いfeature branchの内容を、依存タスク統合済みbaseの代用にする。

作業場所の作成やGit権限で失敗した場合は、コードを変更せず司令役へ報告する。司令役が正式worktreeを作成するまで待つ。

## 完了前確認

- 指定されたDebug/Release buildと全testを実行する。
- `git diff --check`を実行する。
- 自分の報告書と台帳の自分の行だけを更新する。
- Result commitを実hashで報告し、`HEAD`や「git log参照」で代用しない。
- `push`、`merge`、`rebase`は行わない。
