# ビルド基準

## 必要環境

現行プロジェクトは `net6.0-windows` と WPF を対象とする。ビルドとテストには、次のいずれかの開発環境が必要である。

- .NET 6 SDK（`dotnet --list-sdks` に6.xが表示されること）とWindows Desktop targeting pack
- Visual Studio 2022の「.NETデスクトップ開発」ワークロード、および.NET 6のSDK/targeting pack

インストール済みruntime（`Microsoft.NETCore.App`や`Microsoft.WindowsDesktop.App`）だけでは、`Microsoft.NET.Sdk`を解決できないためビルドできない。
リポジトリの `global.json` でSDK `6.0.428`、`rollForward=disable`を固定し、両scriptは実行前に`dotnet --version`が一致することを検証する。

## 基準コマンド

リポジトリルートから次のコマンドを実行し、終了コード、SDK version、警告数、エラー数、成果物を記録する。

```powershell
dotnet --version
powershell -File eng/build.ps1 -Configuration Debug
powershell -File eng/build.ps1 -Configuration Release
powershell -File eng/test.ps1
```

SDK準備前のTASK-000実行時点では、上記コマンドはすべて `Not verified` であり、成功扱いにしなかった。SDK準備後にこれらのscriptを基準コマンドとして実行した。

## TASK-000実行結果

- SDK: .NET SDK `6.0.428`（x64、ユーザー領域）
- Debug build: 成功、0 warnings、0 errors
- Release build: 成功、0 warnings、0 errors
- test: 成功、8 tests passed、0 failed、0 skipped
- Release smoke起動: 3秒間プロセス継続を確認。UI操作自体は未確認

追加した基準資材は `tests/MojiCollaTool.Tests`、`eng/build.ps1`、`eng/test.ps1`、`global.json`、baseline/performanceのmctzip root、及び日本語path fixtureである。

## Fixture定義

- blurなしbaseline: 横書き1文字、縦書き1文字、Unicode文字object、回転、背景box、二重outline、Image1/Image2（右配置）を含む。
- blurありperformance dataset: baselineと同じ構成で、一次outline blur `8`、二次outline blur `4`を含む。
- `tests/MojiCollaTool.Tests/Fixtures/日本語フォルダー/現行プロジェクト.mctzip`で、日本語directory/file名からのarchive読込を検証する。
- fixture自動testはroot entry、CanvasData、MojiDataの方向・文字数・回転・outline・blur・背景box・Image2・Unicodeを具体的に検証する。

## 性能基準

基準端末、計測方法、実測値、閾値は `docs/testing/performance-baseline.md` に記録する。測定対象はWPF描画そのものではなく、blurありmctzipの展開とCanvasData/MojiData読込のheadless proxyである。
