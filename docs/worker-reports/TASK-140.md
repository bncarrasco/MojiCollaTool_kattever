# TASK-140 Auto Text Layout Implementation Report

## Result

TASK-140 now provides a deterministic, visual-only, grapheme-aware layout plan. `FullText` is never rewritten by wrapping, and CR/LF/CRLF explicit breaks remain explicit visual breaks. Advanced language-specific prohibition rules and hyphenation remain out of scope.

`FitTextToBalloon` changes only the linked text's persisted font size/position plus its visual plan. `FitBalloonToText` changes only the balloon position/bounds while preserving linked text, tail geometry, and attached-symbol data. Both are explicit one-way operations and each invocation is one history boundary.

## C140 review coverage

- C140-01: `BindPage`, page/project switching, `ReloadBoundPage`, Undo/Redo rebinding, and versioned save/reload rebuild a visual plan from persisted geometry, font, and `TextLinkData` without creating history or dirtying the document.
- C140-02: pre-commit service/trial failures restore the bound `PageDocument`, Canvas, selection, z-order, attached symbols, computed plans, and live visuals. Subscriber exceptions are outside the rollback boundary and therefore propagate while retaining the semantic commit.
- C140-03: the UI exposes current padding and minimum-font values. It rejects empty, non-numeric, NaN, Infinity, negative padding, non-positive minimum size, minimum size above the current font, and overlarge padding with Japanese status text and no mutation. Valid mode/alignment/padding/minimum values commit together.
- C140-04: FitTextToBalloon gates only the text target; FitBalloonToText gates only the balloon target. The fixed side may be locked or hidden and is still measured.
- C140-05: direction/alignment mapping and target text/balloon geometry are returned by `TextLayoutService`; `PageEditorControl` applies the result.
- C140-06: both modes, no-op, explicit breaks, tail preservation, attached symbols, two directions, one-entry Undo/Redo, redo branches, dirty/saved state, and rebind plan/canvas behavior are covered.
- C140-07: unit, lifecycle, UI, persistence, compatibility, and performance coverage is included below.

## Git scope

- Task: `TASK-140`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-140`
- Branch: `feature/TASK-140-auto-text-layout`
- Functional start HEAD: `6adc3c9affd1cc72fec3dfc7633e8794bdb05b3c`
- Previous implementation commits retained: `fdbf417` and `96b1629f5f9b8663558644f5c1bb9689f7f76c22`
- Review-fix commit: `faf756c3bfca72dc740ca3a3596bd198b99b9744`
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` = `6.0.428`
- Push, merge, and rebase: not performed

## Changed files

- `MojiCollaTool/MojiCollaTool/Layout/TextLayoutService.cs`
- `MojiCollaTool/MojiCollaTool/MojiPanel.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `MojiCollaTool/MojiCollaTool/Document/BalloonData.cs`
- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `tests/MojiCollaTool.Tests/TASK140AutoTextLayoutTests.cs`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`
- `CHANGELOG.md`

## Verification

- Debug build: pass, 0 warnings, 0 errors
- Debug test: 202 passed, 0 failed
- Release build: pass, 0 warnings, 0 errors
- Release test: 202 passed, 0 failed
- `git diff --check`: pass
- Measured layout performance: Debug p95 `0.015 ms`, batch24 `0.302 ms`; Release p95 `0.017 ms`, batch24 `0.246 ms` (Release budgets: `<100 ms` / `<1000 ms`; Debug safety budget: `<3000 ms`).
- Automated coverage: grapheme wrap, explicit CR/LF/CRLF breaks, surrogate/combining/ZWJ content, two directions, two fonts, minimum-font behavior, bounded cache, FullText immutability, service-owned alignment, FitTextToBalloon/FitBalloonToText invariants, lifecycle rebuild, persistence, UI validation, atomic rollback, subscriber propagation, no-op, tail, attached symbols, lock/visibility targeting, dirty/saved state, and Undo/Redo
- Manual WPF pointer/DPI/IME/final-pixel inspection: not verified

## Known boundaries

- The layout policy remains explicit: text edit, drag, or resize does not implicitly reapply a previous plan.
- FormattedText falls back to a finite approximation if a font cannot be constructed; the bounded measure cache remains capped at 2048 entries.
- Advanced prohibition rules, hyphenation, blur effects, and future reactive layout remain outside TASK-140.

## Rollback

Create a revert commit for the TASK-140 changes on this branch if rollback is requested. Do not push, merge, or rebase.
