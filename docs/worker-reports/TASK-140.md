# TASK-140 自動テキストレイアウト 実装報告

## Result

TASK-140を実装しました。`TextLayoutService`がFullTextを変更せずに書記素単位の横書き・縦書き基本wrapと明示改行保持を行い、`MojiPanel`はcomputed layout planだけを表示へ適用します。高度な禁則、ハイフン分割、言語別line-break engineは対象外です。

`FitTextToBalloon`はフキダシのX/Y、Bounds、Rotation、tailを固定し、リンク文字のfont size・表示位置・visual planだけを更新します。`FitBalloonToText`はリンク文字のFullText・X/Y・font sizeを固定し、paddingを含む実測結果でフキダシのX/Y・Boundsだけを更新します。tail tipはpage座標のまま保持します。

UIではmode、alignment、明示Applyを追加しました。locked/hidden/invalid/no-linkは日本語statusで拒否し、同一結果はno-opです。適用はtrial capture後に1回のContentChangedとしてProjectSessionの1 Undo entryへ記録し、失敗時はvisual/modelを復元します。

## 対応要件・ADR

- `REQ-LAYOUT-001`
- `REQ-UNDO-001`
- `REQ-NFR-PERF-001`
- `REQ-NFR-UNICODE-001`
- `ADR-0008`

## Git開始確認

- Task: `TASK-140`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-140`
- Branch: `feature/TASK-140-auto-text-layout`
- Functional base / 開始HEAD: `6adc3c9affd1cc72fec3dfc7633e8794bdb05b3c`
- 開始時status: clean
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` = `6.0.428`
- push / merge / rebase: 実行していません

## 変更ファイル

- `MojiCollaTool/MojiCollaTool/Layout/TextLayoutService.cs`
- `MojiCollaTool/MojiCollaTool/MojiPanel.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `tests/MojiCollaTool.Tests/TASK140AutoTextLayoutTests.cs`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`
- `CHANGELOG.md`
- `docs/worker-reports/TASK-140.md`

## Verification

- Debug build: pass、警告0・エラー0
- Debug test: 195 passed、0 failed
- Release build/test: 最終確認前
- `git diff --check`: pass
- 自動検証: basic wrap、CRLF、surrogate/combining/ZWJ、縦書き、minimum font、bounded cache、FullText不変、FitTextToBalloon一履歴Undo/Redo
- 手動WPF pointer/DPI/IME/final-pixel: Not verified

## 既知の制限・後続影響

- FormattedTextを使えないfont環境では有限の近似measureへfallbackします。cache容量は既定2048です。
- 自動再計算は行わず、drag/resize/text edit後は再度Applyする明示policyです。
- TASK-150/160の追加tail、TASK-180のblur、連続reactive layout、full組版engineへは拡張していません。

## Rollback

本branchのTASK-140変更を対象にrevert commitを作成してください。push、merge、rebaseは行いません。
