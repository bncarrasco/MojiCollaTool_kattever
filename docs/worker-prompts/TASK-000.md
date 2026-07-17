# TASK-000 ワーカー指示書

## 役割と作業場所

あなたはビルド基準・characterization test harness担当です。他機能を実装しません。

- Branch: `feature/TASK-000-build-baseline`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-000`
- Base: tag `plan-base-20260717`。開始時に `git rev-parse HEAD` と `git rev-parse plan-base-20260717`が一致することを記録する。
- 最初に `AGENTS.md`、本指示書、current-state-review、requirements、verification-matrixを読む。

## 背景・目的・要件

現環境は.NET runtimeだけがありSDKがないため、Debug/Releaseとも `Microsoft.NET.Sdk`未解決で失敗した。REQ-NFR-BUILD-001、REQ-NFR-TEST-001、REQ-NFR-PERF-001に対し、後続workerが同じcommandとfixtureで検証できる基準を作る。

## Scope

- 必要SDK/Visual Studio componentをREADMEまたはbuild文書へ明記する。
- Debug/Release/testを1 commandずつ実行できる最小scriptを用意する。
- 適切なtest frameworkとtest projectを追加し、solutionへ登録する。
- `CanvasData`, `ImageData`, `MojiData` XML round-tripのcharacterization testを追加する。
- 再配布可能な自作fixtureとして現行mctzip root構造を固定する。原作者releaseから無断転載しない。
- verification matrixのbaseline scenarioと実行結果を更新する。
- `.gitignore`の `bin/obj`不足は、変更が必要なら理由を報告し、product機能と混ぜない。

## Scope外

- .NET target framework upgrade、DataIO bug修正、新保存形式、MainWindow/XAML変更、UI追加、性能最適化、外部サービスCIの有効化。

## 変更領域と競合回避

- 変更可: solution/csproj、new `tests/`, new `eng/`、build文書、自task report/ledger/matrix行。
- なるべく変更しない: product C#、XAML、README、CHANGELOG、他task planning。
- 公開interfaceを変えずにtest不能なら、変更前に司令役へ報告する。
- TASK-190AはREADME/noticeを変更するため触れない。

## UI・保存・互換性

- UI影響なし。新しいuser-facing UI文字列を追加しない。
- 現行formatの観測値をcharacterizationとして固定し、望ましい新仕様と混同しない。
- XML/zip fixtureに横書き、縦書き、日本語、改行を含める。

## 受け入れ・検証

- 必須: Debug build、Release build、全test。command、SDK version、警告/エラー数をreportへ記録。
- SDKがないままなら、systemへ無断installせずBlockedとして報告し、未検証設定を成功扱いしない。
- 手動起動は可能なら実施。UI日本語/互換性は変更なしとして影響を確認する。
- testが現行bugを示す場合、bugを直さずexpected characterizationか明示的skip理由を記録する。

## 完了処理

- `docs/worker-reports/TASK-000.md`を作成し、自分のledger/matrix行だけを更新する。
- meaningfulな英語commit messageで自branchへcommitする。push/merge/rebaseしない。
- 最終報告: Result、commit hash、変更file、build/test、fixture、既知risk、後続TASK-005/010への注意、rollback方法。
