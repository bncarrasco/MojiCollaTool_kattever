# 検証マトリクス

`Not verified`を成功扱いしない。Verified task/commitは統合後に更新する。

| Feature | Requirement | Build | Automated test | Manual verification | Vertical | Horizontal | Japanese UI | Compatibility | Verified task/commit | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| App startup | baseline | Debug/Release pass (0 warnings, 0 errors) | 8 suite tests passed; startup automationなし | Smoke started 3s; UI操作はNot verified | N/A | N/A | Not verified | N/A | TASK-000 / `939c457` | Fixed SDK 6.0.428; integrated |
| New edit | baseline | Not verified | None | Not verified | N/A | N/A | Mostly Japanese by inspection | N/A | - | current single page |
| Load background JPG/PNG | baseline | Not verified | None | Not verified | N/A | N/A | Filter英語 | Current behavior only | - | file/drag-drop pathあり |
| Add second image | baseline | Not verified | None | Not verified | N/A | N/A | Japanese by inspection | Current format image2 | - | placement 4方向 |
| Add text | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | - |
| Move text | baseline | Not verified | None | Not verified | Not verified | Not verified | N/A | X/Y round-trip未検証 | - | drag direct mutation |
| Rotate/resize text | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | - |
| First/second outline | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | blur性能risk |
| Background box | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | rounded rectangleあり |
| Save current project | SAVE/COMPAT | Debug/Release pass; save未検証 | None | Not verified | Not verified | Not verified | Filter英語 | Risk: non-atomic/stale XML | - | TASK-005対象 |
| Reload current project | COMPAT | Not verified | None | Not verified | Not verified | Not verified | Filter英語 | Risk: state loss on failure | - | TASK-005/041 |
| Japanese path | UNICODE | Debug/Release pass | 8 passed | Not verified | N/A | N/A | N/A | Japanese path archive verified | TASK-000 / `939c457` | `日本語フォルダー/現行プロジェクト.mctzip` |
| Corrupt project load | ERROR | Not verified | None | Not verified | N/A | N/A | Error日本語 | Current state loss risk | - | - |
| Save failure preserves old file | SAVE | Not verified | None | Not verified | N/A | N/A | Not verified | Current code fails design review | - | fault test required |
| Multiple pages | PAGE | Debug/Release pass (0 warnings, 0 errors) | 71 passed | Not verified | Tategaki data保持 | Yokogaki data保持 | 新規tab/操作文言は日本語 | versioned/legacy、page asset隔離 | TASK-030 / `7fc27d5` | CRUD、順序、active page、3page保存、page複製asset、STA tab選択を検証 |
| Multiple project tabs | WORKSPACE | Debug/Release pass (0 warnings, 0 errors) | 71 passed | Not verified | N/A | N/A | 新規tab/close文言は日本語 | session/path/asset隔離 | TASK-030 / `7fc27d5` | 二段tab、2project×3page、duplicate path、個別close基盤を検証 |
| Dirty state | DIRTY | Debug/Release pass (0 warnings, 0 errors) | 71 passed | Not verified | N/A | N/A | dirty印とclose文言は日本語 | saved/current revision、page dirty、失敗時保護 | TASK-030 / `7fc27d5` | captureと変更通知を分離。Undo統合はTASK-070 |
| Undo/Redo | UNDO | Not implemented | None | Not verified | Not verified | Not verified | Required | history not saved | - | TASK-070 |
| Z-order/lock | ZORDER/LOCK | Debug/Release pass; 0 warnings/0 errors | Debug/Release 79 passed, 0 failed | Not verified | Not verified | Not verified | Required by TASK-080 | UUID/Type/ZIndex/lock/visibility/relationship fields; canonical page order, clone remap, legacy XML defaults, and versioned round-trip covered | TASK-060 / `e1af174` | Integrated。UI operations remain TASK-080 |
| Grapheme handling | UNICODE | Debug/Release pass (0 warnings, 0 errors) | Debug/Release 137 passed, 0 failed | UI/render Not verified | variation/modifier/RI pairs/ZWJ/CRLF/empty fixtures | Japanese/emoji text round-trip | N/A: model lane | Grapheme service exposes UTF-16 ranges and lossless reconstruction | TASK-090 | UI integration remains TASK-100 |
| Attached symbols | SYMBOL | Debug/Release pass (0 warnings, 0 errors) | Debug/Release 172 passed, 0 failed | WPF manual pointer/DPI/IME/final-pixel inspection Not verified | parent/anchor/offset/inheritance/decoration/spacing/detached round-trip | SetMojiDatas、orphan policy、cross-object ID atomicity、Undo/Redo、2.1 read compatibility、UI capture/reanchor/selection、detached save/reload、hidden rendering、実描画mixed Z-order、grapheme refresh baseline、24-symbol refresh/drag measurement、ScrollViewer viewport、UI invalid-input rejection | Required by TASK-100 | page format 2.2, 2.0/2.1 reader compatibility, future 2.3 rejection | TASK-090/TASK-100 | TASK100AttachedSymbolUiTests 17 tests: grapheme edge cases, all candidates/arbitrary text, two recognized fonts and both directions, placement/parent transform, font fallback/name, decoration/spacing, mutually-exclusive selection, Japanese UI boundary rejection, ScrollViewer measure/arrange, UI add/remove/property/drag history, redo branch/saved dirty, capture loss, page/project/selection/Dispose, detached preservation, IsVisible=false, rendered mixed canonical Z-order with save/reload, refresh/drag performance |
| Basic balloon model/rendering | BALLOON | Debug/Release pass (0 warnings, 0 errors) | Debug/Release 142 passed, 0 failed | Manual WPF interaction/DPI inspection Not verified | Tategaki text relationship round-trip | Yokogaki/Unicode text relationship round-trip | Japanese shape selector/add/history description | page.xml version 2.2, version 2.0/2.1 read compatibility, future version rejection, geometry fallback/cache | TASK-110/TASK-120/TASK-090 | T120 rendering/selection/move/8-direction resize/history implemented; tail/text link UI remains TASK-130 |
| Tail/text link model | BALLOON | Debug/Release pass (0 warnings, 0 errors) | Debug 116 passed, 0 failed | UI editing Not verified | Tail/link data round-trip | Tail/link data round-trip | N/A: model-only task | Single-tail slot, unlink-on-text-delete policy, Set/Undo/Redo/persistence mixed-order regression | TASK-110 | Interactive tail/text-link UI remains TASK-130 |
| Auto layout | LAYOUT | Not implemented | None | Not verified | Not verified | Not verified | Required | Mode fields | - | TASK-140 |
| Clipboard image | CLIPBOARD | Not implemented | None | Not verified | N/A | N/A | Required | Background persistence | - | TASK-170 |
| Current/all page export | EXPORT | Debug/Release pass (0 warnings, 0 errors) | 109 passed, 0 failed | GUI手動操作はNot verified。STA renderer integrationはverified | PNG/JPEGの論理寸法、CanvasColor維持、透明部分の白合成を検証 | active page復元はMainWindow実装、serviceはpage単位継続 | 日本語dialog/error/summary | 既存project形式変更なし | TASK-050 / report参照 | 順序、zero padding、日本語/無効文字/予約名、衝突、無効出力先、不透明/半透明/完全透明JPEGを検証 |
| Fork identity | FORK | Debug/Release pass (0 warnings, 0 errors) | Debug/Release 73 passed | MainWindow title、ヘルプ→このアプリについて、Aboutの非公式フォーク・原作者非関係・問い合わせ注意、AssemblyTitle/package metadataを確認 | LICENSE/THIRD-PARTY-NOTICES.mdをDebug/Release/publishへ同梱 | 4 ColorSelector filesにWpfColorPicker/Apache-2.0由来・local changes通知を確認 | AboutからLICENSE/THIRD-PARTY-NOTICES.mdを案内 | Root MIT retained; copyright `2024 kuramiya` retained; Company/Authorsの製品名設定を削除 | TASK-190B / `d727b8b` | Integrated at `b80402e`。installer/ZIP/manual app launchはscope外/Not verified |
| XML round-trip characterization | NFR-TEST/UNICODE | Debug/Release pass | 8 passed (3 XML + 5 fixture/perf) | Not verified | Tategaki fixture verified | Yokogaki fixture verified | N/A: UI変更なし | Current XML fixture verified | TASK-000 / `939c457` | Integrated。`CanvasData`/`ImageData`/`MojiData`、改行、surrogate pair、結合濁点、emoji sequence |
| mctzip root fixture | NFR-TEST/COMPAT | Debug/Release pass | 8 passed | Not verified | N/A | N/A | N/A: UI変更なし | Root entries/XML read/archive/Japanese path verified | TASK-000 / `939c457` | Integrated。baseline/performance自作fixture。Image1/Image2、MojiData1/2/3 |
| Fixture load performance baseline | NFR-PERF-001 | Debug/Release pass | 1 p95 test passed | Not verified | N/A | N/A | N/A: UI変更なし | Current performance fixture | TASK-000 / `939c457` | 9 iterations, p95 89.387 ms, threshold ≤500 ms。WPF描画は対象外 |

## Baseline datasets

TASK-000で再配布可能性を確認し、以下を固定する。

- 画像2枚、横書き/縦書き各1文字object、Unicode object、回転、背景box、2重outline、blurなし。
- 同構成で一次blur 8、二次blur 4の性能dataset。
- Image2を右配置したCanvasData。
- CRLF/LF、日本語、ASCII、surrogate pair、結合濁点、emoji sequenceを含むtext。
- 日本語directory/file名に置いた現行mctzip（`日本語フォルダー/現行プロジェクト.mctzip`）。
- MojiDataを削除した後の保存、破損zip、unknown XML field、欠落画像。

## TASK-005 verification update

| Feature | Requirement | Automated verification | Result | Manual/UI |
| --- | --- | --- | --- | --- |
| Safe legacy save | SAVE/COMPAT | Fresh workspace, stale-entry exclusion, readback validation, replace | Pass | Not verified |
| Safe legacy load | COMPAT/ERROR | Corrupt zip, traversal, staged XML validation, Working commit point | Pass | Not verified |
| Image-backed save/load | SAVE/COMPAT | Explicit Image1/Image2 copy, archive round-trip, decoder/dimension validation | Pass | Not verified |
| Image deletion / broken image | SAVE/ERROR | Stale image exclusion, corrupt image rejection, existing Working preservation | Pass | Not verified |
| Duplicate image extensions | COMPAT/ERROR | Image1/Image1 duplicate-extension archive rejection | Pass | Not verified |
| Persistence error messages | ERROR/UI | Japanese save/load/Working outer messages; internal exception retained in log builder | Pass | Not verified |

## Manual baseline procedure

1. 起動し画像を新規読込。
2. 横書きと縦書きを追加し、移動、回転、size、2重outline、blur、背景boxを設定。
3. PNG/JPEGを書き出し寸法を確認。
4. mctzip保存、終了、再読込し目視比較。
5. 日本語pathで繰り返す。
6. 破損fileを開き、既存sessionとfileが保持されることを確認（TASK-005後）。
