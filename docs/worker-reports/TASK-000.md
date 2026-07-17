# TASK-000 ワーカー報告

## Result

**Blocked** — .NET SDKが利用できないため、ビルド基準とcharacterization test harnessの検証を完了できなかった。SDKのインストールは行っていない。

## Summary

- 開始時のHEADと `plan-base-20260717` は一致した。
- `dotnet --list-sdks` は空で、runtimeのみがインストールされている。
- SDKを必要とする製品ビルド、テストプロジェクトのrestore/build、fixtureを使ったround-trip testは実行可能な状態にできないため、未検証の実装を追加しなかった。
- SDK要件と基準コマンドを `docs/testing/build-baseline.md` に記録した。
- `docs/testing/verification-matrix.md` と `docs/planning/implementation-ledger.md` のTASK-000範囲だけを更新した。

## Scope / requirements

- 対応要件ID: `REQ-NFR-BUILD-001`, `REQ-NFR-TEST-001`, `REQ-NFR-PERF-001`
- TASK-000以外の製品機能、UI、保存形式、DataIOの修正は行っていない。
- テストframework、test project、script、mctzip fixture、XML round-trip characterization testは、SDK blockerのため未作成。

## Branch / worktree / commits

- Branch: `feature/TASK-000-build-baseline`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-000`
- Base commit: `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a`
- Result commit: この報告を含むcommit（hashは最終handoffに記載）
- push / merge / rebase: 実施していない

## Changed files

- `docs/testing/build-baseline.md`
- `docs/testing/verification-matrix.md`
- `docs/planning/implementation-ledger.md`
- `docs/worker-reports/TASK-000.md`

## Design decisions / ADR

SDKがない状態でテストframeworkやNuGet依存を追加すると、restore・コンパイル・実行を確認できない。したがって今回のBlocked結果では、公開interface、product C#、XAML、solution、`.gitignore`を変更しない判断とした。新規ADR候補はない。

## Verification

### SDK

- `dotnet --list-sdks`: **Not verified / Blocked** — 出力なし
- `dotnet --list-runtimes`: .NET 6.0.11 / 9.0.17のruntimeおよびWindowsDesktop runtimeを確認
- `dotnet --version`: **Not verified / Blocked** — `No .NET SDKs were found.`

### Build / test

| Command | Result | Diagnostic |
| --- | --- | --- |
| `dotnet build MojiCollaTool/MojiCollaTool.sln --configuration Debug --nologo` | **Blocked** (exit 1) | `The application 'build' does not exist` / `No .NET SDKs were found` |
| `dotnet build MojiCollaTool/MojiCollaTool.sln --configuration Release --nologo` | **Blocked** (exit 1) | `The application 'build' does not exist` / `No .NET SDKs were found` |
| `dotnet test MojiCollaTool/MojiCollaTool.sln --configuration Debug --nologo` | **Blocked** (exit 1) | `The application 'test' does not exist` / `No .NET SDKs were found` |

コンパイル段階へ到達していないため、compiler warning/error数、成果物、test件数は判定不能である。成功扱いにしていない。

### Manual / UI / compatibility

- Debug/Release起動: **Not verified** — SDK blocker
- 縦書き・横書き、日本語UI: **Not verified** — UI変更なしだがアプリ未起動
- XML/zip互換性、日本語path、fixture round-trip: **Not verified** — harness未作成
- `bin/obj`除外: product側 `MojiCollaTool/.gitignore` に既存設定があるため変更不要

## Known risks and follow-up

- SDKが準備されるまで、REQ-NFR-BUILD-001/TEST-001/PERF-001の受入条件は未達である。
- TASK-005/010は、このTASK-000のSDK準備、test project、fixture、round-trip testの完了後に着手すること。今回のBlocked状態を検証済み基準とみなさない。
- SDK準備後は、本報告の基準コマンドを再実行し、test framework、test project、script、fixture、characterization testを追加してマトリクスを更新する。

## Deviation / rollback

- 指示されたtest harness一式を追加できなかった。理由は、必要SDKが存在せず、未検証のビルド・テストを成功扱いしないという指示に従ったためである。
- ロールバックはこのcommitで追加した4ファイルをrevertすることで可能。履歴改変は行っていない。
