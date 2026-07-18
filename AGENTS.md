# MojiCollaTool 勝手版 — 作業規則

> **Fork-specific governance:** This file contains development instructions specific to the unofficial MojiCollaTool Kattever fork. It is not part of the upstream project and does not imply endorsement by or affiliation with the original author.

## Project identity

- プロジェクト名は「MojiCollaTool 勝手版」とする。
- kuramiya氏によるMojiCollaToolの非公式フォークであり、原作者・公式版とは無関係であることを明示する。
- 勝手版に関する問い合わせを原作者へ送らないよう案内する。
- 元作者の著作権表示とMITライセンスを維持する。
- 取り込まれた第三者コードのライセンスと帰属表示を維持する。

## Repository publication policy

- 本ファイルは非公式forkの公開可能な開発規約としてGit管理する。上流projectの規約や原作者による承認を表すものではない。
- 司令役用の詳細manual、司令役が新規生成する個別worker指示書、一時的なAI分析・作業memoはlocal AI referenceとし、`.git/info/exclude`等で除外して、`git add`、commit、push、merge、配布物への同梱を行わない。
- 要件、ADR、設計文書、worker報告書、実装台帳、CHANGELOG、source、testなど、人間の開発・review・保守にも必要な成果物は通常どおりGit管理する。
- local AI referenceにも秘密情報、credential、token、不要な個人情報を記録しない。
- 既にGit履歴へ存在する過去のworker指示書を、この方針だけを理由に履歴改変して削除しない。今後新規作成するlocal指示書から適用する。

## Language policy

- ユーザー向けUI、エラー、警告、ダイアログ、メニュー、ボタン、ツールチップ、状態表示は日本語にする。
- コード識別子、内部キー、ブランチ名、worktree名、コミットメッセージは原則として英語にする。
- 設計文書、ワーカー指示書、報告書、ADRは日本語にする。
- ユーザーが入力した文字列、ページ名、ファイル名は改変せず保持する。

## Product principles

- 軽量な文字コラ・漫画組版・フキダシ配置・縦書き・少数ページのシナリオ作成に特化する。
- 汎用画像編集ソフト化、不要な依存追加、全面書き換え、複雑なUIを避ける。
- 既存機能、旧プロジェクト形式、縦書き、日本語パス、Unicode文字を常に考慮する。
- 機能追加とリファクタリングを分け、レビュー可能でビルド可能な単位を維持する。
- 保存処理では既存ファイルの保護を優先し、未検証の互換性を保証済みと表現しない。

## Git worktree bootstrap

- 実装作業は、司令役が指定したbranch、worktree、functional base commit、開始時HEADでのみ開始する。開始時HEADはtask指示書または司令役のkickoff指示にexact hashで示す。
- 正式なworktreeは `git worktree add` で作成され、主repositoryの `git worktree list --porcelain` に登録されている必要がある。repositoryフォルダーのコピーはworktreeとして扱わない。
- repository全体、`.git` directory、linked worktreeの `.git` fileをコピーしない。コピー先で `git init`、偽の `.git` 作成、共有Git管理領域の手動編集を行わない。
- Git metadata権限、`dubious ownership`、worktree作成失敗が発生した場合は、実装を始めず司令役へ報告する。フォルダーコピーや未管理作業コピーへ切り替えてはならない。
- 自分の `docs/worker-prompts/TASK-XXX.md` が存在しない、またはbranch/worktree/functional base/開始時HEADの指定がない場合は開始せず司令役へ報告する。
- 最初の編集前に、次を実行して指示書の期待値と一致することを確認する。
  - `git rev-parse --show-toplevel`
  - `git branch --show-current`
  - `git rev-parse HEAD`
  - `git status --short`
  - `git worktree list --porcelain`
- `git status --short`は開始時にcleanでなければならない。例外が必要な場合は、変更所有者と扱いを司令役へ確認する。
- 依存タスクはfunctional baseへ統合済みであることを確認する。開始時HEADがfunctional baseより後の場合、その差分は司令役が明示した指示書・運用文書等に限定する。他feature branchや古い作業コピーをbaseの代用にしない。
- 上記確認結果を作業報告の「開始時Git確認」へ記録する。

## Worker rules

- 最初に本ファイル、`docs/worker-prompts/README.md`、自分の `docs/worker-prompts/TASK-XXX.md` を読む。
- 割り当てられたタスクだけを実施し、スコープ外の改善や先行実装を行わない。
- 指示書と実コードに矛盾があれば、実装前に司令役へ報告する。
- ユーザーの未コミット変更、他worktree、他ワーカーのブランチを変更しない。
- `push`、`merge`、`rebase`、force操作、履歴改変を行わない。
- リファクタリングと新規依存は必要最小限にする。
- 新規UI文言は日本語にし、縦書きと横書きの双方を確認する。
- 保存形式変更時は旧形式読込、未知バージョン、失敗時保護、日本語パスを確認する。
- 作業後に指定されたDebug/Releaseビルド、テスト、手動確認を行う。
- 実施できない検証は理由とともに `Not verified` と記録する。
- `docs/worker-reports/TASK-XXX.md` と、自分の行だけの `implementation-ledger.md` 更新をコミットへ含める。
- `CHANGELOG.md`にはユーザー向けに意味のある変更だけを記録し、コミット一覧を転記しない。

## Build and test SDK

- 本repositoryの標準SDKは `global.json` が指定する .NET SDK 6.0.428 とする。
- この環境では `C:\Users\user\.dotnet\dotnet.exe` を使用する。PATH上の `dotnet` が見つからない、または別versionを返すことだけを理由にSDK未導入と判断しない。
- 作業開始時に `C:\Users\user\.dotnet\dotnet.exe --version` を実行し、`6.0.428`であることを確認して報告書へ記録する。
- 通常の検証はrepository標準scriptを使用する。
  - Debug build: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Debug`
  - Release build: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Release`
  - Test: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\test.ps1`
- script外で`dotnet`を直接実行する必要がある場合も、`C:\Users\user\.dotnet\dotnet.exe`を明示する。
- SDKの再インストール、更新、`global.json`変更はタスクscopeに含めず、必要に見える場合は実施前に司令役へ報告する。

## Completion report

ワーカー報告には、Result、Summary、対応要件ID、ブランチ、worktree、Base/Result commit、変更ファイル、設計判断、ADR候補、指示からの逸脱、ビルド・テスト・手動確認、縦書き・横書き・日本語UI・互換性確認、既知の問題、後続影響、マージ注意、ロールバック方法を含める。
