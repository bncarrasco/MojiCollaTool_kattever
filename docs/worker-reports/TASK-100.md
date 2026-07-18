# TASK-100 Attached Symbol UI / Grapheme Rendering Report

## Result

- Task: `TASK-100`
- Branch: `feature/TASK-100-attached-symbol-ui`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-100`
- Functional base: `4402a6101c457a615bb86412981954d1ea1e0171`
- SDK: `6.0.428` (`C:\Users\user\.dotnet\dotnet.exe`)

## Implementation

- PageEditor now keeps all attached-symbol models separately from parent-bound visuals. Detached models are retained through parent deletion, page/project switching, Capture, and versioned save/load.
- Capture validates the complete candidate state on a cloned PageDocument before mutating the bound page. Add validates text, ID, parent, grapheme anchor, scale, and finite numeric values before changing the parent visual, model, or history.
- Attached-symbol drags use a unique gesture coalesce key. Pointer-up creates one history entry per drag; capture loss restores the pre-gesture state.
- `IsVisible=false` and detached visuals now have no rendered child geometry and are hidden/non-hit-testable.
- Candidate UI includes `濁点`, `半濁点`, arbitrary text, `装飾継承`, and `文字間隔に含める`. The settings area is hosted by a real `ScrollViewer`; font choices continue to use `FontUtil` system-font enumeration.
- Grapheme visual pooling now keys by the complete grapheme string, including variation selectors, modifiers, ZWJ sequences, and regional-indicator pairs.

## Automated verification scope

`TASK100AttachedSymbolUiTests` contains 10 tests covering:

- variation selector, skin tone, ZWJ, two flags, CRLF, and empty-line grapheme layout;
- horizontal/vertical placement and parent movement, size, and rotation follow-up;
- all built-in candidates plus arbitrary text;
- inheritance, decoration, spacing, font fallback, saved font name, and hidden rendering;
- one drag per history entry, two drags producing two Undo entries, and capture-loss rollback;
- page switching, selection restoration, Dispose, detached preservation after parent deletion, save/reload, and mixed canonical Z-order;
- atomic rejection of empty text, overlong text, out-of-range anchor, and duplicate ID;
- full-grapheme refresh performance baseline.

Existing TASK-090 model tests additionally cover cross-object ID validation, orphan policies, persistence compatibility, and model-level atomicity.

## Verification

- `C:\Users\user\.dotnet\dotnet.exe --version`: pass (`6.0.428`).
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Debug`: pass, 0 warnings / 0 errors.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\test.ps1`: pass, 165 passed / 0 failed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Release`: pass, 0 warnings / 0 errors.
- `C:\Users\user\.dotnet\dotnet.exe test .\tests\MojiCollaTool.Tests\MojiCollaTool.Tests.csproj -c Release --no-build`: pass, 165 passed / 0 failed.
- `git diff --check`: pass.
- Final `git status --short --branch`: clean after the result commits.

## Manual verification boundary

Automated tests do not replace interactive WPF pointer, DPI, IME, or final-pixel inspection on every installed font. Those remain manual verification items; the report does not claim them as automated coverage.

## Handoff

TASK-130 remains ordered after TASK-100 because both work in PageEditor. No push, merge, or rebase was performed.
