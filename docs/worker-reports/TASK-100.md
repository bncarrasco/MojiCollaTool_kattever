# TASK-100 付加記号UIと書記素描画 実装報告

## Result

- Task: `TASK-100`
- Branch: `feature/TASK-100-attached-symbol-ui`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-100`
- Functional base / kickoff HEAD: `4402a6101c457a615bb86412981954d1ea1e0171`
- Result implementation commit: `4727e170d62d0fd71f0991187c3108dd5b68b4f5`
- SDK: `6.0.428`（`C:\Users\user\.dotnet\dotnet.exe`）

## Implementation

- `DecoratedCharacterControl`と`MojiPanel`を`GraphemeService`の書記素単位へ接続し、サロゲート、結合文字、ZWJ sequenceを1 visualとして描画するようにした。
- `AttachedSymbolVisual`とem基準の`AttachedSymbolPlacement`を追加し、親textの書記素anchor、位置、倍率、回転、縦書き／横書き、親回転へ追従させた。
- `MojiWindow`へ対象書記素、候補記号（`!`、`?`、`!?`、`!!`、`?!`）、任意入力、X/Y、倍率、回転、継承、個別font、fallback状態、削除の日本語UIを追加した。
- PageEditorへ付加記号の追加・選択・削除・property update・dragを接続した。dragはpointer-upで1履歴、capture lossでbeforeへ復元し、親文字削除時はdetached状態を保持する。
- PageEditor capture時にvisual編集をモデルへ反映してからtext anchor reconciliationを行い、本文編集、page/project切替、Undo/Redo、保存復元でvisualとselectionを再構成するようにした。
- UI更新値はcandidate validationを経由し、不正なID、scale、anchorが文書へ流れないようにした。

## Verification

- Kickoff Git: top-level、branch、HEAD、clean status、worktree登録を確認。
- SDK: pass（`6.0.428`）。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Debug`: pass、0 warnings / 0 errors。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\test.ps1`: pass、157 passed / 0 failed。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Release`: pass、0 warnings / 0 errors。
- `C:\Users\user\.dotnet\dotnet.exe test .\tests\MojiCollaTool.Tests\MojiCollaTool.Tests.csproj -c Release --no-build`: pass、157 passed / 0 failed。
- `git diff --check`: pass。
- TASK-100 tests: 書記素visual数、付加記号UI capture、本文編集時の再anchor、Undo/Redoを自動検証。

## Known issues / manual verification

- 実WPF画面でのマウス操作、DPI別の見た目、フォントごとの縦書きgolden image確認はこの環境では未実施（Not verified）。
- フォントfallbackはシステムfont一覧照合とfallback状態表示を自動検証対象とし、端末ごとの実フォント外観確認は未実施。

## Handoff / rollback

- TASK-130はPageEditorのballoon tail/text-link操作を本taskの後に接続する。
- rollbackは本taskのResult implementation commitをrevertすれば、書記素visual、付加記号visual/UI、PageEditor接続、tests/docsの変更をまとめて戻せる。
