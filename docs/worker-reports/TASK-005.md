# TASK-005 実装報告

## Result

**Complete** — 旧単ページ `.mctzip` の保存・読込を一時領域経由に変更し、既存ファイルと現在のWorkingデータを失わないコミット境界を追加しました。

## Summary

- 保存は共有Workingディレクトリを入力にせず、空の一時ディレクトリへ `Info.txt`、`CanvasData.xml`、`MojiData*.xml` を全量生成する。
- CanvasDataが参照する現在の `Image1.*` / `Image2.*` を保存用一時領域へ明示コピーし、参照しない画像や古い画像entryは保存しない。
- 同一番号の画像が複数拡張子で存在する場合は拒否し、CanvasDataとの存在・画像寸法整合性とWPF decoderによる復号可能性を置換前に検証する。
- zipを閉じた後、中央ディレクトリ、entry重複、パス traversal、entry数・展開サイズ、必須entry、XMLを検証してから保存先を置換する。
- 保存先は先に削除せず、`File.Replace` と安全な overwrite fallback を使用する。既定ではバックアップを作らず、既存APIに任意バックアップ指定を追加した。
- 読込はOS一時領域へ展開して検証し、成功後にWorkingと同じ親ディレクトリ内の一時領域へコピーしてWorkingを置換する。破損・未知の危険entryでは既存Workingを変更しない。
- MainWindowは読込検証・Working反映後に画面状態を置換する順序へ変更した。

## Requirements / ADR

- 対応: `REQ-SAVE-001`, `REQ-COMPAT-001`, `REQ-NFR-ERROR-001`
- 参照: `ADR-0005-versioned-project-format-and-legacy-import.md`
- TASK-005では旧root形式の安全化までを対象とし、manifest/versioned multi-page formatはTASK-040、legacy mappingはTASK-041の対象とした。

## Branch / worktree / base

- Branch: `feature/TASK-005-safe-legacy-persistence`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-005`
- Base: `939c457` (`feature/TASK-000-build-baseline`)
- Code result commit: `99086e0` (`Implement safe legacy project persistence`)

## Changed files

- `MojiCollaTool/MojiCollaTool/DataIO.cs`
- `MojiCollaTool/MojiCollaTool/MainWindow.xaml.cs`
- `tests/MojiCollaTool.Tests/LegacyPersistenceTests.cs`
- `CHANGELOG.md`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`

## Verification

| Command | Result |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\test.ps1` | Pass: 17 passed, 0 failed; performance p95 8.623 ms / threshold 500 ms |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Debug` | Pass: 0 warnings, 0 errors |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng\\build.ps1 -Configuration Release` | Pass: 0 warnings, 0 errors |
| `git diff --check` | Pass |

自動検証には、現行fixture、Unicode、日本語パス、画像付き保存・読込、画像削除後のstale除去、重複拡張子拒否、破損画像保持、既存archive置換、破損zip保持、traversal拒否、Working commitを含む。実UIでの保存・読込操作は未実施（Not verified）。

## Known risks / follow-up

- 旧形式の読込・安全化であり、新manifest/version gateや複数ページ形式は未実装。
- `MainWindow`のエラーダイアログ文言と手動UI確認は後続の統合レビューで確認する。
- RollbackはTASK-005の実装コミットをrevertする。
