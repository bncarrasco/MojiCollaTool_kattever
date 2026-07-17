# TASK-000 ワーカー報告

## Result

**Complete** — SDK準備後にビルド基準、characterization test harness、現行mctzip root fixtureを追加し、Debug/Release/testを検証した。

## Summary

- 開始時のHEADと `plan-base-20260717` は一致した。
- SDK準備前はBlockedとして停止し、SDKの無断インストールは行わなかった。司令役の追加指示後、公式の.NET 6 SDK 6.0.428をユーザー領域へ導入した。
- MSTest test project、Debug/Release/test script、固定SDK、baseline/performanceの現行mctzip root fixture、XML round-trip characterization testを追加した。
- `docs/testing/build-baseline.md`、`docs/testing/verification-matrix.md`、`docs/planning/implementation-ledger.md`をTASK-000範囲で更新した。

## Scope / requirements

- 対応要件ID: `REQ-NFR-BUILD-001`, `REQ-NFR-TEST-001`, `REQ-NFR-PERF-001`
- TASK-000以外の製品機能、UI、保存形式、DataIOの修正は行っていない。
- MSTestをtest-only依存として採用した。product C#、XAML、DataIO、公開interfaceは変更していない。

## Branch / worktree / commits

- Branch: `feature/TASK-000-build-baseline`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-000`
- Base commit: `adf5fd8fb9e34deb9fdd2cba373f28c8c3f22a4a`
- Result commit: 最終handoffに記載（報告書自身へhashを埋め込まない）
- push / merge / rebase: 実施していない

## Changed files

- `docs/testing/build-baseline.md`
- `docs/testing/verification-matrix.md`
- `docs/planning/implementation-ledger.md`
- `docs/worker-reports/TASK-000.md`
- `MojiCollaTool/MojiCollaTool.sln`
- `tests/MojiCollaTool.Tests/MojiCollaTool.Tests.csproj`
- `tests/MojiCollaTool.Tests/XmlRoundTripTests.cs`
- `tests/MojiCollaTool.Tests/PerformanceBaselineTests.cs`
- `tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/*`
- `tests/MojiCollaTool.Tests/Fixtures/current-mctzip.mctzip`
- `tests/MojiCollaTool.Tests/Fixtures/performance-mctzip-root/*`
- `tests/MojiCollaTool.Tests/Fixtures/performance-mctzip.mctzip`
- `tests/MojiCollaTool.Tests/Fixtures/日本語フォルダー/現行プロジェクト.mctzip`
- `tests/.gitignore`
- `eng/build.ps1`
- `eng/test.ps1`
- `global.json`
- `docs/testing/performance-baseline.md`

## Design decisions / ADR

SDK準備後にMSTestをtest-only依存として追加した。`global.json`のSDK 6.0.428とscript内のversion checkでビルド環境を固定した。既存公開interfaceを変えず、ProjectReferenceでproduct assemblyを対象にXML serializerの観測値を固定した。test build生成物を追跡しないため、`tests/.gitignore`に`bin/obj`を追加した。新規ADR候補はない。

## Verification

### SDK

- `C:\Users\user\.dotnet\dotnet.exe --version`: **Verified** — `6.0.428`
- `C:\Users\user\.dotnet\dotnet.exe --list-sdks`: **Verified** — `6.0.428`
- 既存の.NET 6.0.11 / 9.0.17 runtimeおよびWindowsDesktop runtimeも確認した。
- `global.json`: **Verified** — SDK `6.0.428`, `rollForward=disable`
- `eng/build.ps1`, `eng/test.ps1`: **Verified** — version checkが一致しないSDKを拒否する。

### Build / test

| Command | Result | Diagnostic |
| --- | --- | --- |
| `powershell -File eng/build.ps1 -Configuration Debug` | **Pass** (exit 0) | 0 warnings, 0 errors; `MojiCollaTool.dll` and test DLL generated |
| `powershell -File eng/build.ps1 -Configuration Release` | **Pass** (exit 0) | 0 warnings, 0 errors; `MojiCollaTool.dll` and test DLL generated |
| `powershell -File eng/test.ps1` | **Pass** (exit 0) | 8 passed, 0 failed, 0 skipped; performance p95 89.387 ms / threshold 500 ms |

build/testはユーザーSDKを明示的に使用し、未検証を成功扱いしていない。

### Manual / UI / compatibility

- Debug/Release起動: **Verified smoke** — Release exeを起動し3秒間プロセス継続を確認後、起動プロセスを終了した。
- 縦書き・横書き、日本語UI: **Not verified** — UI操作・目視確認は実施していない。fixture/testにはTategaki、Yokogaki、日本語を含めた。
- XML/zip互換性、日本語path、fixture round-trip: **Verified within current format** — baseline/performance root、Image2、blur有無、回転、背景box、二重outline、Unicode、日本語path、mctzip archive entryを自動確認。公式版との互換性は保証しない。
- 性能基準: **Verified for headless proxy** — 9回測定のp95 89.387ms、許容閾値500ms。WPF描画・ドラッグ応答は未計測。
- `bin/obj`除外: product側設定に加え、新規test projectの生成物を除外する`tests/.gitignore`を追加した。

## Known risks and follow-up

- REQ-NFR-BUILD-001、REQ-NFR-TEST-001、REQ-NFR-PERF-001のTASK-000定義範囲は満たした。PERFはheadless fixture load proxyであり、WPF描画・ドラッグ応答の性能退行を保証しない。
- UIの縦書き・横書き操作、日本語UIの目視確認は未実施である。
- TASK-005/010は、この結果commitのレビュー・統合後に開始可能である。

## Deviation / rollback

- SDK blocker期間中は中間成果`e77c550a943726f2848dc943fc7b533892d3eee9`を維持し、SDK準備後に同じブランチで作業を再開した。
- ロールバックはこの結果commitの変更をrevertすることで可能。push、merge、rebase、履歴改変は行っていない。
