# 検証マトリクス

`Not verified`を成功扱いしない。Verified task/commitは統合後に更新する。

| Feature | Requirement | Build | Automated test | Manual verification | Vertical | Horizontal | Japanese UI | Compatibility | Verified task/commit | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| App startup | baseline | Debug/Release pass (0 warnings, 0 errors) | 8 passed | Smoke started 3s; UI操作はNot verified | N/A | N/A | Not verified | N/A | - | Locally verified with fixed SDK 6.0.428; integration pending |
| New edit | baseline | Not verified | None | Not verified | N/A | N/A | Mostly Japanese by inspection | N/A | - | current single page |
| Load background JPG/PNG | baseline | Not verified | None | Not verified | N/A | N/A | Filter英語 | Current behavior only | - | file/drag-drop pathあり |
| Add second image | baseline | Not verified | None | Not verified | N/A | N/A | Japanese by inspection | Current format image2 | - | placement 4方向 |
| Add text | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | - |
| Move text | baseline | Not verified | None | Not verified | Not verified | Not verified | N/A | X/Y round-trip未検証 | - | drag direct mutation |
| Rotate/resize text | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | - |
| First/second outline | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | blur性能risk |
| Background box | baseline | Not verified | None | Not verified | Not verified | Not verified | Japanese by inspection | Current MojiData | - | rounded rectangleあり |
| Save current project | SAVE/COMPAT | Failed: SDK missing | None | Not verified | Not verified | Not verified | Filter英語 | Risk: non-atomic/stale XML | - | TASK-005対象 |
| Reload current project | COMPAT | Not verified | None | Not verified | Not verified | Not verified | Filter英語 | Risk: state loss on failure | - | TASK-005/041 |
| Japanese path | UNICODE | Debug/Release pass | 8 passed | Not verified | N/A | N/A | N/A | Japanese path archive verified | - | `日本語フォルダー/現行プロジェクト.mctzip` |
| Corrupt project load | ERROR | Not verified | None | Not verified | N/A | N/A | Error日本語 | Current state loss risk | - | - |
| Save failure preserves old file | SAVE | Not verified | None | Not verified | N/A | N/A | Not verified | Current code fails design review | - | fault test required |
| Multiple pages | PAGE | Not verified: SDK missing | Not verified: ProjectDocumentTests 6 tests（SDK missing） | Not verified | Not verified | Not verified | N/A: UI変更なし | Model-only; format unchanged | TASK-010（integration pending） | Project/Page/Canvas model、CRUD、順序、deep clone、legacy adapterを実装。新形式・tabsはTASK-030/040 |
| Multiple project tabs | WORKSPACE | Not implemented | None | Not verified | Not verified | Not verified | Required | session isolation | - | two-level tabs |
| Dirty state | DIRTY | Not implemented | None | Not verified | N/A | N/A | Required | N/A | - | TASK-070/030 |
| Undo/Redo | UNDO | Not implemented | None | Not verified | Not verified | Not verified | Required | history not saved | - | TASK-070 |
| Z-order/lock | ZORDER/LOCK | Not implemented | None | Not verified | Not verified | Not verified | Required | New fields | - | TASK-060/080 |
| Grapheme handling | UNICODE | Current char-unit risk | None | Not verified | Not verified | Not verified | N/A | Round-trip unknown | - | TASK-090 |
| Attached symbols | SYMBOL | Not implemented | None | Not verified | Not verified | Not verified | Required | New fields | - | TASK-090/100 |
| Basic balloon | BALLOON | Not implemented | None | Not verified | Not verified | Not verified | Required | New fields | - | TASK-110/120 |
| Tail/text link | BALLOON | Not implemented | None | Not verified | Not verified | Not verified | Required | Relationships | - | TASK-130 |
| Auto layout | LAYOUT | Not implemented | None | Not verified | Not verified | Not verified | Required | Mode fields | - | TASK-140 |
| Clipboard image | CLIPBOARD | Not implemented | None | Not verified | N/A | N/A | Required | Background persistence | - | TASK-170 |
| Current/all page export | EXPORT | Current only | None | Not verified | Not verified | Not verified | Required for all-pages | N/A | - | TASK-050 |
| Fork identity | FORK | N/A | N/A | README/app/distribution not verified | N/A | N/A | Required | License review open | - | TASK-190A/B |
| XML round-trip characterization | NFR-TEST/UNICODE | Debug/Release pass | 8 passed (3 XML + 5 fixture/perf) | Not verified | Tategaki fixture verified | Yokogaki fixture verified | N/A: UI変更なし | Current XML fixture verified | - | Locally verified; integration pending。`CanvasData`/`ImageData`/`MojiData`、改行、surrogate pair、結合濁点、emoji sequence |
| mctzip root fixture | NFR-TEST/COMPAT | Debug/Release pass | 8 passed | Not verified | N/A | N/A | N/A: UI変更なし | Root entries/XML read/archive/Japanese path verified | - | Locally verified; integration pending。baseline/performance自作fixture。Image1/Image2、MojiData1/2/3 |
| Fixture load performance baseline | NFR-PERF-001 | Debug/Release pass | 1 p95 test passed | Not verified | N/A | N/A | N/A: UI変更なし | Current performance fixture | - | 9 iterations, p95 89.387 ms, threshold ≤500 ms。WPF描画は対象外 |

## Baseline datasets

TASK-000で再配布可能性を確認し、以下を固定する。

- 画像2枚、横書き/縦書き各1文字object、Unicode object、回転、背景box、2重outline、blurなし。
- 同構成で一次blur 8、二次blur 4の性能dataset。
- Image2を右配置したCanvasData。
- CRLF/LF、日本語、ASCII、surrogate pair、結合濁点、emoji sequenceを含むtext。
- 日本語directory/file名に置いた現行mctzip（`日本語フォルダー/現行プロジェクト.mctzip`）。
- MojiDataを削除した後の保存、破損zip、unknown XML field、欠落画像。

## Manual baseline procedure

1. 起動し画像を新規読込。
2. 横書きと縦書きを追加し、移動、回転、size、2重outline、blur、背景boxを設定。
3. PNG/JPEGを書き出し寸法を確認。
4. mctzip保存、終了、再読込し目視比較。
5. 日本語pathで繰り返す。
6. 破損fileを開き、既存sessionとfileが保持されることを確認（TASK-005後）。
