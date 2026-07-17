# TASK-000 性能基準

## 基準端末

測定日: 2026-07-17

| 項目 | 値 |
| --- | --- |
| Host | `DESKTOP-R1UNR34` |
| OS | Windows build `10.0.26200`, 64-bit（OS API表示: Microsoft Windows 11 Home） |
| CPU | AMD Ryzen 7 3800X 8-Core Processor |
| Logical processors | 16 |
| Memory | 約64 GiB |
| GPU | 取得API権限の制約により未記録 |
| SDK | .NET SDK 6.0.428 x64 |

## 計測方法

`PerformanceBaselineTests.PerformanceFixtureLoadP95IsWithinThreshold`が、`performance-mctzip.mctzip`を9回それぞれ一時directoryへ展開し、`CanvasData.xml`と3件の`MojiData*.xml`を読み込む経過時間を`Stopwatch`で測定する。初回を除外せず、9サンプルのp95を判定値とする。

このdatasetはblurあり、Image2右配置、横書き・縦書き・Unicode objectを含む。測定はファイル展開/XML読込の基準であり、WPFのGeometry/Blur描画、起動後のドラッグ応答、GPU性能は対象外である。

## 基準値と許容閾値

| Metric | Observed | Threshold | Result |
| --- | ---: | ---: | --- |
| performance fixture load p95 | 89.387 ms | ≤ 500 ms | Pass |

再現コマンドは `powershell -File eng/test.ps1`。scriptは`global.json`の6.0.428固定を確認してからtestを実行する。
