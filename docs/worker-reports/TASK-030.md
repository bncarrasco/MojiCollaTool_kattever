# TASK-030 作業報告

## Result

実装完了。`MainWindow`をworkspace shellへ接続し、上段project tab／下段page tabで複数project・複数pageを同一アプリ内で扱えるようにしました。画像はglobal `Working`を正本にせず、`ProjectSession`ごとの一時asset storeへpage ID単位で隔離しました。

## Summary

- 起動時に「無題」projectと`01` pageを作成し、「新規作成」は既存sessionを破棄せず新しいproject tabを追加。
- page追加・複製・削除・名前変更・左右移動、最後の1ページ削除拒否、page/project dirty印、対象session単位のclose確認を追加。
- `DataIO.ReadProject(path, sink)`をsession asset storeへ接続し、読込失敗時は既存workspaceを変更しないtransaction境界を維持。
- `DataIO.WriteVersionedProject(path, document, assetSource)`を使い、保存成功後だけFilePath設定と`MarkSaved`を実行。
- 同一pathの相対表記・絶対表記の重複をworkspaceの既存path正規化で拒否し、既存tabをactive化。
- `PageEditorControl`へ最小限のpage bind/capture/unbindを追加し、active pageだけを表示。
- page複製時に画像bytesを新PageIdへ複製し、page削除・session closeでasset storeを解放。
- captureと実変更通知を分離し、cleanなtab切替・保存・bind/unbindではdirtyを発生させないよう修正。
- MojiPanelのdrag完了、MojiWindow、CanvasEditWindow、文字追加削除、画像変更からContentChangedを通知。
- RefreshTabsはTabItem.Tagとactive modelの参照一致でSelectedItemを復元。
- 画像候補をdecode・bytes検証してからpage assetを一括置換し、失敗時にasset・metadata・表示を復元。

## 対応要件・ADR

- 要件: `REQ-PAGE-001`, `REQ-WORKSPACE-001`, `REQ-DIRTY-001`, `REQ-UI-JA-001`
- ADR: `ADR-0001`, `ADR-0002`
- asset判断: `global Working`は複数session/pageのasset正本として共有せず、`ProjectSessionAssetStore`がsession UUID配下のpage UUIDディレクトリを所有する。batch restoreはstage作成後のディレクトリ置換で一括反映する。

## 開始時Git確認

- top-level: `F:/github/MojiCollaTool-worktrees/TASK-030`
- branch: `feature/TASK-030-project-page-tabs`
- HEAD: `70386485419c3d9dbcb09ee9c2c7493e5e66b50b`
- status: clean
- worktree登録: 主repositoryの`git worktree list --porcelain`に指定pathとbranchが登録済み
- functional base `8875af4696663f34a6ba72f255b2f9e209578dd2`は開始時HEADの祖先

## Branch / worktree / commit

- Branch: `feature/TASK-030-project-page-tabs`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-030`
- Base commit: `8875af4696663f34a6ba72f255b2f9e209578dd2`
- 開始時HEAD: `70386485419c3d9dbcb09ee9c2c7493e5e66b50b`
- Result commit: `124e3e4ff0883dcffeaecc6d28cf724abc291b39`
- Review fix commit: `d7ec6847d33c06a118f6339425566ee4843bd6d9`

## Changed files

- `MojiCollaTool/MojiCollaTool/MainWindow.xaml`
- `MojiCollaTool/MojiCollaTool/MainWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `MojiCollaTool/MojiCollaTool/CanvasEditWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/MojiPanel.cs`
- `MojiCollaTool/MojiCollaTool/MojiWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/Document/PageDocument.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/ApplicationWorkspace.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/ProjectSession.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/ProjectSessionAssetStore.cs`
- `tests/MojiCollaTool.Tests/ProjectSessionAssetStoreTests.cs`
- `tests/MojiCollaTool.Tests/TASK030ReviewTests.cs`
- `CHANGELOG.md`
- `docs/planning/implementation-ledger.md`
- `docs/worker-reports/TASK-030.md`

## Verification

| Command | Result |
| --- | --- |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Debug` | Pass: 0 warnings, 0 errors |
| `powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Release` | Pass: 0 warnings, 0 errors |
| `powershell.exe -ExecutionPolicy Bypass -File eng\test.ps1` | Pass: 71 passed, 0 failed |
| `git diff --check` | Pass |

追加テストはcaptureとdirtyの分離、MojiPanel drag／MojiWindow／Canvas編集通知、active tab復元、画像2枚の失敗時保護、2project×3page隔離、保存成功後のdirty解除、close拒否を確認します。既存60件を維持し、TASK-030追加11件を含む71件です。

## Manual / UI / compatibility

- 二段tab、縦書き／横書き切替、画像D&D、保存・再読込、個別close、アプリ終了の実Window操作: `Not verified`（この実行では自動testとDebug/Release buildのみ実施）。
- 新規UI文言は日本語で追加し、page名・project名・本文は入力値をtrim・正規化せず保持する実装。
- versioned 形式は既存reader/writerを使用。legacy形式は`ProjectReadWarning`を表示し、通常保存時にversioned形式へ保存。
- 日本語path・Unicode値は既存reader/writerのテスト範囲を維持。新UIの実手動確認は未実施。

## Known risks / downstream impact

- `PageEditorControl`の既存MojiWindowが直接変更する`MojiData`は変更イベント後にcaptureするadapter方式。Undo/Redo本体はTASK-070の範囲。
- 旧global Workingを直接参照するlegacy UI経路はTASK-030のworkspace shellからは使用しないが、旧形式互換API自体は後続互換用途のため残存。
- UIの実STA操作確認は未実施。TASK-050/TASK-170はこの二段tab・session/page境界を前提に後続実装する。

## 指示からの逸脱

- `requirements.md`は指示書記載の直下pathには存在せず、実在する`docs/planning/requirements.md`の指定REQ項目を確認した。
- push、merge、rebase、履歴改変は実施していない。

## Rollback

TASK-030のResult commitをrevertすれば、MainWindow shell、PageEditor接続、session asset store、自動テスト、報告・台帳・CHANGELOGの変更をまとめて戻せます。既存のTASK-010/020/025/040/041の形式・reader/writer実装は変更していません。
