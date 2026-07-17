# TASK-010 作業報告

## Result

実装完了。ProjectDocument／PageDocument／CanvasDocumentを追加し、既存の単ページ`CanvasData`・`MojiData`を文書モデルへ取り込むlegacy adapterとdeep clone境界を実装した。UI、MainWindow、既存保存形式、新serializerは変更していない。

## Summary

- プロジェクトは常に1ページ以上を保持し、初期ページ名は`01`、追加ページは`02`形式とした。
- ページIDはUUIDで生成し、ページ追加・複製・削除・名前変更・挿入・並べ替えを提供する。
- `ProjectDocument(Guid, name, pages)`は入力ページのPageIdを保持したままdeep copyし、重複PageIdを拒否する。
- ページ順序は0始まりの`Order`へ操作後に正規化する。
- ページ複製、プロジェクト複製、legacy import/exportはCanvasDataとMojiDataをdeep copyし、元データとの参照共有を防ぐ。
- 既存`CanvasData.Copy`が配置位置をコピーしないため、document境界のcloneで`Image2LocatePosition`を明示的に保持する。
- MojiDataのlegacy整数IDは現段階ではページ内値として保持し、共通オブジェクトID／ZIndexはTASK-060へ委ねる。

## 対応要件・設計

- 要件: REQ-PAGE-001, REQ-WORKSPACE-001
- ADR: ADR-0001
- Scope外: tabs、ProjectSession、UI抽出、新形式serializer、MainWindow変更
- 指示書からの逸脱: 専用`docs/worker-prompts/TASK-010.md`がworktreeに存在しなかったため、`task-breakdown.md`、ADR-0001、requirementsを原典として実施した。

## Branch / worktree / commits

- Branch: `feature/TASK-010-project-page-model`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-010`
- Base: `939c4576884a05b0c265ab8cb42240f8dd1e88ae`（TASK-000基準完了commit）
- Result commits: `c3040b5`（model implementation）, `b6e85f7`（clone name semantics）, `ef8230a`（identity validation）

## 変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/CanvasDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/PageDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/ProjectDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/LegacyProjectDataAdapter.cs`
- `tests/MojiCollaTool.Tests/ProjectDocumentTests.cs`
- `docs/planning/implementation-ledger.md`（TASK-010行のみ）
- `docs/testing/verification-matrix.md`（Multiple pages行のみ）
- `docs/worker-reports/TASK-010.md`

## Build / test / manual verification

- `dotnet --info`: .NET SDK `6.0.428`を確認。
- `powershell -ExecutionPolicy Bypass -File eng/build.ps1 -Configuration Debug`: 成功、0 warnings、0 errors。
- `powershell -ExecutionPolicy Bypass -File eng/build.ps1 -Configuration Release`: 成功、0 warnings、0 errors。
- `powershell -ExecutionPolicy Bypass -File eng/test.ps1`: 成功、16 tests passed、0 failed、0 skipped。ProjectDocumentTestsは8件。
- SDK blocker: 解消。通常のPowerShell実行はExecutionPolicyで拒否されたため、検証時は`-ExecutionPolicy Bypass`を使用した。
- `git diff --check`: pass。
- 手動UI: Not verified。UI変更なし。
- 縦書き／横書き: `ProjectDocumentTests`と既存fixtureでMojiData値の保持を確認。
- 互換性: 旧XML／mctzipの形式・DataIOは変更していない。既存XML/mctzip fixtureを含む16テストが成功。

## Known risks / downstream impact

- `CanvasData`と`MojiData`はWPF型・既存mutable型のままであり、完全immutable化はしていない。後続serializerは`CanvasDocument.ToLegacyData()`または明示adapterを使用すること。
- MojiDataの安定UUID、ZIndex、他オブジェクト型は未実装。TASK-060でページモデルとの境界を確認すること。
- ProjectSession／dirty／history／tabsは未実装。TASK-025、TASK-030が本モデルAPIを利用する。
- 手動ページ操作と統合レビューは未実施。UI変更はないが、後続task統合時に確認すること。

## Rollback / merge notes

このcommitをrevertすれば、Documentモデル、adapter、モデルテスト、TASK-010の台帳・検証・報告をまとめて戻せる。MainWindow、solution、既存DataIOは変更していないため、後続taskとの競合は新規Document配下を中心に確認する。push、merge、rebaseは行っていない。
