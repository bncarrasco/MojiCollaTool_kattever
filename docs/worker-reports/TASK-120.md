# TASK-120 フキダシ描画と編集handle 実装報告

## Result

TASK-120を実装しました。geometry/cache、4種shape描画、選択、移動、8方向resize、drag preview/cancel、PageEditorへの追加導線を含みます。

## Git / environment

- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-120`
- Branch: `feature/TASK-120-balloon-editor`
- Functional base: `384dcfc76ef39871daa47c91ef6bd5085afafc33`
- Implementation commit: `7feee3c`
- SDK: `6.0.428` (`C:\Users\user\.dotnet\dotnet.exe`)

## Implementation

- `BalloonGeometryFactory`はEllipse/RoundedRectangle/Rectangle/Monologueを生成し、未知shapeは矩形へfallbackします。
- cache keyはshapeとBoundsのX/Y/Width/Heightを含み、容量256のLRU evictionを行います。生成Geometryはfreezeして共有します。
- `BalloonVisual`はFill/Stroke/StrokeThickness/Visibility/Rotationを描画へ反映し、選択時の枠を表示します。
- `PageEditorControl`に日本語shape selectorと「フキダシ追加」を追加し、クリック選択、移動、8方向resize、locked object除外、zoom時のhandleサイズ補正を実装しました。
- 移動・resizeはpointer downで状態を保存し、move中はvisualだけをpreview、pointer upでContentChangedを1回通知します。capture lossはbeforeへ復元します。Tail geometry/handle、linked text一体移動、lock UIはTASK-130/TASK-080以降の範囲です。

## Verification

- Debug build: pass, 0 warnings / 0 errors
- Debug test: pass, 142 passed / 0 failed
- Release build: pass, 0 warnings / 0 errors
- Release test: pass, 142 passed / 0 failed
- `git diff --check`: pass
- Automated tests cover selection/background clearing, move, all eight resize directions, minimum size, zoom handle behavior, capture-loss cancel, separate rapid gesture undo entries, undo/redo/redo-branch/saved-dirty, mixed Z-order, page/project switching, save/reload, all four shape geometries, unknown fallback, cache hit/miss/eviction, and warm-cache performance.
- Geometry cache performance: 10,000 warm-cache calls in 1.106 ms on Release; threshold 500 ms; 10,999 hits and 1 miss.
- 履歴説明を「フキダシ位置・サイズ変更」に正規化し、未接続の旧イベントメソッドと未使用4引数コンストラクターを削除。プロジェクト全体（生成物・画像を除く194ファイル）を文字化け候補で走査し、テキストファイルのヒットは0件。
- Manual WPF pointer interaction and visual DPI inspection: not run in this environment.

## Handoff / rollback

TASK-130はtail/root/width handleとtext link一体操作を追加する際、`PageEditorControl`のballoon input boundaryを拡張してください。rollbackは本taskの実装commitをrevertすれば、geometry/visual/PageEditor/test/docsの変更をまとめて戻せます。
