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
dotnet build MojiCollaTool/MojiCollaTool.sln --configuration Debug --nologo
dotnet build MojiCollaTool/MojiCollaTool.sln --configuration Release --nologo
dotnet test MojiCollaTool/MojiCollaTool.sln --configuration Debug --nologo
```

TASK-000の実行時点ではSDKが存在しないため、上記コマンドはすべて `Not verified` であり、成功扱いにしない。

## 未完了の基準資材

SDKが利用可能になった後、TASK-000の同一作業範囲でテストプロジェクト、Debug/Release/testの最小実行script、現行mctzip root fixture、および `CanvasData`、`ImageData`、`MojiData` XML round-trip characterization testを追加し、検証マトリクスを再更新する。
