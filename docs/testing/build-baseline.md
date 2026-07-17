# ビルド基準

## 必要環境

現行プロジェクトは `net6.0-windows` と WPF を対象とする。ビルドとテストには、次のいずれかの開発環境が必要である。

- .NET 6 SDK（`dotnet --list-sdks` に6.xが表示されること）とWindows Desktop targeting pack
- Visual Studio 2022の「.NETデスクトップ開発」ワークロード、および.NET 6のSDK/targeting pack

インストール済みruntime（`Microsoft.NETCore.App`や`Microsoft.WindowsDesktop.App`）だけでは、`Microsoft.NET.Sdk`を解決できないためビルドできない。

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
- test: 成功、5 tests passed、0 failed、0 skipped
- Release smoke起動: 3秒間プロセス継続を確認。UI操作自体は未確認

追加した基準資材は `tests/MojiCollaTool.Tests`、`eng/build.ps1`、`eng/test.ps1`、`tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root`、`current-mctzip.mctzip`である。
