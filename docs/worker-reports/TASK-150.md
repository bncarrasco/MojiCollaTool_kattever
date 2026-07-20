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
- Implementation / test commit: `0f9a657c89159f94f42a61f04e87d507cf040afc`
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

## 検証

- Debug build: 成功、警告0、エラー0
- Debug全test: 246/246成功、失敗0
- Release build: 成功、警告0、エラー0
- Release全test: 246/246成功、失敗0
- TASK-150専用test: Debug/Release 8/8成功
- `git diff --check`: 成功
- Baseline 238 testsを退行させていません。

専用testでは、strict validation、cross-group重複とatomic failure、flat combine、primary/member削除、clone ID preserve/remap、全member dataのdeep非破壊比較、typed block、lock、history／dirty／Undo/Redo／redo branch、subscriber例外、5 shape、rotation、overlap／touch／containment／non-overlap、tail、内部stroke抑制、transparent gap exact hit、実XAML UI、one merge visual、個別handle抑制、group gesture、途中lock、lifecycle、2.4 round-trip、2.3読込、2.5／invalid拒否、日本語pathを確認しました。

### 性能実測

- Debug: single refresh/hit p95 `0.049 ms`、24 group × 100 batch `85.828 ms`
- Release: single refresh/hit p95 `0.045 ms`、24 group × 100 batch `112.663 ms`
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
