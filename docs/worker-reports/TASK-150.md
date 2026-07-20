# TASK-150 非破壊フキダシ合体 実装報告

## Result

TASK-150を完了しました。2件以上のフキダシをpage-levelのrelationshipとしてflatに合体し、memberの`BalloonData`を変更せず、primary styleによるunion外形と各memberのしっぽを一つのvisualで表示します。解除すると各memberのshape、position、size、rotation、style、tail、text link、layout stateがそのまま再表示されます。

合体groupは、各memberの`balloon + linked text + non-detached attached symbols`を順序付きで結合した一つのtyped Z-order blockです。merge／unmerge／group move／Z-order／member削除は全compositionをlock preflightし、拒否時はmodel、visual、selection、history、dirty、notificationを変更しません。group moveはpreview／commit／cancelと途中lock取消に対応し、成功時は1履歴です。

実XAMLへ日本語の候補ComboBox、「フキダシ合体」「合体解除」を追加しました。合体中はmember original visualのbody／hitを抑制し、merge visualだけをCanvasへ置きます。個別resize／tail handleは表示せず、解除後に編集する旨をstatusへ表示します。

## Summary / 対応要件

- `REQ-BALLOON-MERGE-001`: stable merge ID、primary ID、ordered member IDs、flat combine、non-destructive unmerge。
- `REQ-ZORDER-001`: 全member typed compositionを一つのblockとして4 Z操作へ接続。
- `REQ-LOCK-001`: merge／unmerge／move／Z／削除の全related member lock preflight。
- `REQ-UNDO-001`: semantic command、1操作1履歴、saved dirty、Undo/Redo、redo branch、subscriber例外境界。
- `REQ-UI-JA-001`: 実XAMLの日本語操作、status、拒否理由。raw例外／GUID／内部pathは表示しません。
- `REQ-NFR-PERF-001`: finite/frozen geometry、全入力cache key、上限付きLRU、24 group実測。
- `REQ-NFR-UNICODE-001`: 日本語project/page/path、横書き／縦書きlinkを保持。

## Git情報

- Task: `TASK-150`
- Branch: `feature/TASK-150-balloon-merge`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-150`
- Functional base / 開始HEAD: `c87417f3d235365e4e6d5266997acbf15a6099c7`
- Initial implementation / test commit: `0f9a657ad2a87ad315c81f6cb4887926ad834b9d`
- Review round 1 correction commit: `8aa9a7431891700821b153a7d074ca7230e505b0`
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` = `6.0.428`
- push / merge / rebase: 実施していません
- 開始時確認: top-level、branch、HEAD、clean status、正式worktree登録、変更前diffなしを確認済みです。

## 主な変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/BalloonMergeData.cs`
- `MojiCollaTool/MojiCollaTool/Document/PageDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/ProjectDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/ProjectHistoryState.cs`
- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `MojiCollaTool/MojiCollaTool/Visuals/BalloonMergeGeometryFactory.cs`
- `MojiCollaTool/MojiCollaTool/Visuals/BalloonMergeVisual.cs`
- `MojiCollaTool/MojiCollaTool/Visuals/BalloonVisual.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/BalloonMergeCommands.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/BalloonCommands.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `tests/MojiCollaTool.Tests/TASK150BalloonMergeTests.cs`
- `tests/MojiCollaTool.Tests/BalloonPersistenceTests.cs`
- `tests/MojiCollaTool.Tests/VersionedProjectPersistenceTests.cs`

## 設計判断

- merge dataはpage objectではなくrelationship dataとし、`IPageObjectData.GroupId`を使用しません。
- primary groupがある場合はそのprimaryとstable merge IDを維持します。selected standaloneを既存groupへ合体する場合はselected balloonをprimaryとし、既存groupのmerge IDを維持します。
- group bodyは全member bodyをpage座標へ変換してWPF geometry unionし、fillと外周strokeを一度だけ描画します。各tailは保持したままprimary styleでbodyより先に描画します。
- cache keyはMergeIdだけでなくordered member ID、shape、position、bounds、rotation、tail ID／tip／root／widthを含み、結果Geometryをfreezeします。
- version 2.4 writer／minimum readerでmerge collectionを保存します。2.0〜2.3はmergeなしとして読み、2.0〜2.2のlayout state migrationは維持します。invalid 2.4、2.5以降、unknown major、future minimum readerはcommit前に拒否します。

ADR-0009の決定を実装し、ADR-0005／ADR-0008と互換仕様を現行2.4 policyへ更新しました。新しいADR候補はありません。

## Review round 1修正

- 合体中は`FitTextToBalloon`／`FitBalloonToText`のApply commandを直接呼出しと実buttonの双方で拒否し、個別frame resizeを迂回できないようにしました。
- public modelの非force `RemoveBalloon`で、target、他merge member、linked text、non-detached attached symbolのlockを全てpreflightします。
- primaryの`IsVisible`をfill／strokeと同じprimary-style policyとし、hidden primaryでは描画・hitとも無効、hidden non-primaryではprimaryがvisibleならgroup geometryを維持します。
- group moveは全balloon／tail／linked text candidateとrelated symbolをclone上で先に検証し、overflowまたはlate validation failureでは一切commitしません。live gestureも同じmodel trialを通し、不正結果をbefore snapshotへ戻します。
- merge／unmergeの最初の`CapturePage`をrollback範囲へ含め、UI同期validation failureでもdocument、live、Canvas、selection、history、dirty、notificationを復元します。
- 各member×4実Z button、actual Canvas children/Z、全tail同時描画とprimary style、unlinked member移動、Undo/Redo rebind、remove、Dispose後の旧visual eventを追加検証しました。
- review結果: BLOCKER 0、MAJOR 5件を修正、MINOR 1件のcommit hash記録を訂正しました。

## 検証

- Debug build: 成功、警告0、エラー0
- Debug全test: 254/254成功、失敗0
- Release build: 成功、警告0、エラー0
- Release全test: 254/254成功、失敗0
- TASK-150専用test: Debug/Release 16/16成功
- `git diff --check`: 成功
- Baseline 238 testsを退行させていません。review round 1で追加した8件を含め、専用testは16件です。

専用testでは、strict validation、cross-group重複とatomic failure、flat combine、primary/member削除、clone ID preserve/remap、全member dataのdeep非破壊比較、typed block、lock、history／dirty／Undo/Redo／redo branch、subscriber例外、5 shape、rotation、overlap／touch／containment／non-overlap、全member tail同時描画、primary style／visibility、内部stroke抑制、transparent gap exact hit、各memberからの4実Z buttonとCanvas順、実Apply buttonでの合体中layout拒否、model delete全composition lock、overflow／late symbol failure atomic move、linked横／縦＋unlinked member gesture、merge同期失敗rollback、Undo/Redo rebind、remove、Dispose handler解放、2.4 round-trip、2.3読込、2.5／invalid拒否、日本語pathを確認しました。

### 性能実測

- Debug: single refresh/hit p95 `0.045 ms`、24 group × 100 batch `79.505 ms`
- Release: single refresh/hit p95 `0.039 ms`、24 group × 100 batch `62.594 ms`
- Release目標: single p95 50 ms未満、100 refresh batch 1,500 ms未満
- Debug safety: 3,000 ms未満
- geometry cacheはcapacityを超えないことを確認しました。

### 手動確認

- 実WPF pointer操作: Not verified
- OS DPI別表示: Not verified
- IME入力中の操作: Not verified
- final-pixel目視: Not verified
- 自動testでは横書き／縦書き、日本語UI、actual XAML button／ComboBox、visual hierarchy、finite geometryを確認済みです。

## 指示からの逸脱

ありません。TASK-160の複数tail、TASK-230の一般group／multi-select、TASK-200の全面日本語化、新規依存関係、SDK/framework更新は実施していません。

## 既知の境界・後続影響

- 合体中の個別resizeとtail handle編集は意図的に無効です。style、link、layout stateは暗黙変更しません。
- 自動合体、lasso、multi-select、nested merge、一般groupは対象外です。
- 2.4で明示保存したarchiveは2.3以前のreaderでは開けません。2.0〜2.3 archiveを開いただけでは原fileを変更しません。
- TASK-160はmemberが現在保持するsingle tailを拡張する際、merge geometry/cache key/testを更新する必要があります。
- TASK-230はselection UIを参考にできますが、`BalloonMergeData`を一般`GroupId`へ暗黙変換しないでください。

## Merge注意

- `PageEditorControl.xaml(.cs)`、`PageDocument.cs`、`VersionedProjectFormat.cs`はTASK-160／TASK-200／TASK-230と競合し得ます。機能単位で解決し、2.4 gateとtyped compositionを落とさないでください。
- integration branchへのmergeとpushは司令役／オーナー判断です。本taskでは実施していません。

## Rollback

rollbackが必要な場合は、このbranch上でimplementation commitと後続文書commitをrevertするcommitを作成してください。保存済み2.4 archiveを2.3へdowngradeする処理は提供していないため、既存2.4 fileを直接書き換えないでください。
