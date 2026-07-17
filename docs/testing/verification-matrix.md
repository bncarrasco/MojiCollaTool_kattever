# 検証マトリクス

`Not verified`を成功扱いしない。Verified task/commitは統合後に更新する。

| Feature | Requirement | Build | Automated test | Manual verification | Vertical | Horizontal | Japanese UI | Compatibility | Verified task/commit | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| App startup | baseline | Failed: SDK missing | None | Not verified | N/A | N/A | Not verified | N/A | - | SDK解決前 |
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
| Japanese path | UNICODE | Not verified | None | Not verified | Not verified | Not verified | N/A | Not verified | - | fixture必要 |
| Corrupt project load | ERROR | Not verified | None | Not verified | N/A | N/A | Error日本語 | Current state loss risk | - | - |
| Save failure preserves old file | SAVE | Not verified | None | Not verified | N/A | N/A | Not verified | Current code fails design review | - | fault test required |
| Multiple pages | PAGE | Not implemented | None | Not verified | Not verified | Not verified | Required | New format required | - | TASK-010/030/040 |
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
| Fork identity | FORK | N/A: product code unchanged | N/A: docs-only | README wording/link review completed; app/distribution deferred | N/A | N/A | README Japanese; app deferred to TASK-190B | Root MIT retained; WpfColorPicker Apache-2.0 notice added | TASK-190A / `d297bb6` | README and notice scope complete; integration review pending |

## Baseline datasets

TASK-000で再配布可能性を確認し、以下を固定する。

- 画像1枚、横書き/縦書き各1文字object、回転、背景box、2重outline、blurなし。
- 同構成でblurありの性能dataset。
- 画像2枚を上下/左右へ配置したCanvasData。
- CRLF/LF、日本語、ASCII、surrogate pair、結合濁点、emoji sequenceを含むtext。
- 日本語directory/file名に置いた現行mctzip。
- MojiDataを削除した後の保存、破損zip、unknown XML field、欠落画像。

## Manual baseline procedure

1. 起動し画像を新規読込。
2. 横書きと縦書きを追加し、移動、回転、size、2重outline、blur、背景boxを設定。
3. PNG/JPEGを書き出し寸法を確認。
4. mctzip保存、終了、再読込し目視比較。
5. 日本語pathで繰り返す。
6. 破損fileを開き、既存sessionとfileが保持されることを確認（TASK-005後）。
