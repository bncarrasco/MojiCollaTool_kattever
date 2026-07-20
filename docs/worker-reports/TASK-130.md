# TASK-130 しっぽ編集・文字リンク 実装報告

## Result

TASK-130を完了しました。既存`BalloonTailData`／`TextLinkData`を描画・hit test・日本語編集UIへ接続し、single tailの追加／削除、tip／root／width編集、文字link／unlink、typed compositionの一体移動とZ-order、Undo/Redo、保存再読込を実装しました。司令役レビューC130-01～04では、link直後のdocument／live／Canvas順同期、format 2.2重複linkの決定的移行、実UI受入経路、同期rollbackとsemantic event境界を追加確認しました。

## 対応要件

- `REQ-BALLOON-002`
- `REQ-UNDO-001`
- `REQ-ZORDER-001`
- `REQ-NFR-PERF-001`
- ADR-0007

## 開始時Git確認

- Top-level: `F:/github/MojiCollaTool-worktrees/TASK-130`
- Branch: `feature/TASK-130-balloon-tail-link`
- Functional base／開始HEAD: `eb1d45f9ff673ad01d87376877462d14fbd993b7`
- `git status --short`: clean
- `git worktree list --porcelain`: 指定pathとbranchの登録を確認
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` = `6.0.428`
- 開始baseline: Debug／Release 175/175成功

## Commit

- Main implementation: `e2137363ddbccfd34b636628a72d792a99590f73`
- Composition-order expectation update: `88d365a4d15e9e9145a8250cffac6c1b7f470c7e`
- C130-01～04修正＋受入: `3c47087985d95fa65687806b14ded5b4743c91ee`
- Completion metadata: 本報告更新commit（exact hashは完了メッセージに記載）
- Completion metadata: 本報告更新commit（exact hashは完了メッセージに記載）

## 変更ファイル

- `MojiCollaTool/MojiCollaTool/Visuals/BalloonTailGeometry.cs`
- `MojiCollaTool/MojiCollaTool/Visuals/BalloonVisual.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `MojiCollaTool/MojiCollaTool/Document/PageDocument.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/BalloonCommands.cs`
- `tests/MojiCollaTool.Tests/TASK130BalloonTailLinkTests.cs`
- `tests/MojiCollaTool.Tests/BalloonModelTests.cs`
- `tests/MojiCollaTool.Tests/BalloonPersistenceTests.cs`
- `docs/planning/implementation-ledger.md`
- `docs/testing/verification-matrix.md`
- `CHANGELOG.md`
- `docs/worker-reports/TASK-130.md`

## 設計判断

### Tail座標・root mapping・hit test

- `TipX/TipY`はpage canvas座標のまま保持します。描画時はフキダシ中心回転の逆変換でlocal座標へ移し、rotation後も保存されたpage位置へ復元します。
- `RootParameter`は上を0として時計回りに1周する角度parameterです。shape中心から角度方向へrayを伸ばし、Ellipse／RoundedRectangle／Rectangle／Monologue／Unknown fallbackの実body geometry境界との交点をrootとします。0／0.25／0.5／0.75は上／右／下／左、1は0と同一点です。
- root tangentはparameter前後の境界点から決定し、width baseをその接線上へ配置します。root dragは中心からpointerへの角度を直接parameterへ写像し、mousemoveで全周探索しません。
- tailをFill／Stroke／StrokeThickness付きで本体より先に描き、本体を重ねてroot seamを隠します。hit testはbody／tail geometryのfillとstrokeだけを対象とし、body bounding box全体をhitにしません。
- hidden／lockedではbody resizeとtail handleを表示せず、dragを開始しません。handleはzoomの逆数で補正し、画面上サイズを一定にします。

### Link一対一policy

- link候補は同一pageの文字だけをlegacy ID、ObjectId短縮値、表示例付きで列挙します。
- `PageDocument`のAdd／Set／Update／Normalize境界で、一つの文字が複数フキダシへlinkされないことを検証します。別フキダシの文字を黙って奪わず、日本語で拒否します。
- format 2.2のreader／constructor境界だけは互換移行モードとし、canonical `AllObjects`順で最初のballoon linkを保持し、後続重複をunlinkします。dangling link、ID検証、writerのatomic性は厳格なままです。runtimeのAdd／Set／Update／新規linkは重複を拒否します。
- link／unlinkのvalidation／同期失敗はbefore snapshotからpage instanceをin-place復元し、live data、Canvas child／Z、selectionも再構成します。semantic commit後の`ContentChanged` subscriber例外はcatchせず上位boundaryへ伝播し、失敗statusにはraw `Exception.Message`を出しません。
- invalid／cross-page／削除済みIDはmodel／visual／historyを変更せず拒否します。
- link／unlinkは文字の位置、font、縦横、内容を変更しません。文字削除はフキダシを残してunlinkし、フキダシ削除は文字を残します。

### Composition move／Z-order

- body move中はbody X/Y、tail tip、linked text X/Yへ同じpage-space deltaをpreviewし、pointer-upで1履歴にします。cancel／capture lossは全memberをbeforeへ戻します。
- resizeはbody Boundsだけを変更し、linked textの位置／font／layoutを変更しません。tail handle dragはtail対象fieldだけを変更します。文字単体dragの既存動作は文字だけを移動します。
- canonical内部順は`balloon -> linked text -> linked textのAttachedSymbol`です。tailはBalloonVisual内部でbodyより背面です。link成立時と読込時にtyped blockをフキダシ位置へ正規化します。
- Z操作はpage objectをcomposition block列へ分解してblock単位で移動します。unrelated objectの相対順を維持し、端操作はno-opとしてhistory／dirtyを増やしません。

### History／lifecycle

- drag開始時にballoonとlinked textをsnapshotし、mousemoveではvisual/model previewだけ、pointer-upのContentChanged 1回で既存ProjectSession snapshot historyへcommitします。gestureごとに固有coalesce keyを使い、短時間の別gestureを統合しません。
- page/project切替、Undo/Redo後のreload、Disposeでは既存BindPage／RemoveAll boundaryを使い、tail handleとlink候補を再構成・解放します。
- link／unlinkは`CapturePage`の試行検証後に`ApplyPageOrderToLiveObjects`と`RebuildCanvasObjectOrder`まで同一UI操作内で実行し、通知前からdocument順、live model ZIndex、Canvas.ZIndex、typed child順を一致させます。失敗時はlive linkを復元し、通知・履歴・dirtyを発生させません。
- C130-04ではfaulting subscriber後もsemantic link／unlinkとhistory／dirtyを保持して例外を伝播し、trial validation failureではdocument／live／Canvas／selection／history／dirtyをbeforeへ戻すことを確認しました。UI statusは固定日本語概要のみです。

## Verification

- SDK: 6.0.428
- Debug build: pass、0 warnings／0 errors
- Debug test: 192 passed／0 failed
- Release build: pass、0 warnings／0 errors
- Release test (`--no-build`): 192 passed／0 failed
- test件数差: 175 baseline + TASK-130専用17 = 192（C130-04受入2件を追加）
- `git diff --check`: pass
- format: version 2.2を維持。既存2.0／2.1読込、unknown shape、tailなし、日本語project/page/path、text／symbol／balloon／image／exportを含む全suite成功
- 縦書き: link前後で方向／font／内容を保持するSTA test成功
- 横書き: composition move／resizeで位置以外のfont／内容を保持するSTA test成功
- 日本語UI: しっぽ追加／削除、実ComboBox＋`LinkTextButton`／`UnlinkTextButton`、現在状態、4種Z button、invalid／競合拒否を確認
- C130受入: `RealLinkButtonSynchronizesDocumentLiveZCanvasOrderAndHistory`、`RealZButtonsUseTypedCompositionAndEdgeNoOpDoesNotCreateHistory`、`Version22DuplicateLinksAreMigratedByCanonicalOrderAndRoundTrip`、`PageSwitchRefreshesLinkCandidatesAndDisposeStopsFurtherBinding`、`HiddenBalloonHasNoHandlesAndCannotStartGesture`、`LinkedTextSingleObjectMoveLeavesBalloonGeometryUnchanged`を通過
- C130-04受入: `LinkNotificationSubscriberExceptionPropagatesAfterSemanticCommit`、`LinkTrialValidationFailureRollsBackEditorDocumentCanvasHistoryDirtyAndStatus`を通過
- Undo/Redo: add/remove、実UI link/unlink、tail drag、composition move/Z、別gesture分離、redo branch、saved dirty、cancelを確認

## 性能実測

同一process内の自動計測。対象は24 balloon、tail refresh/hit 100反復（2,400回）、Monologue root parameter 40回、linked composition block move 200回。各閾値は3,000 ms:

- Debug: tail refresh/hit `598.761 ms`、root drag `0.009 ms`、composition block move `83.906 ms`
- Release: tail refresh/hit `424.342 ms`、root drag `0.224 ms`、composition block move `84.613 ms`
- geometry cache count: `4`、capacity `256`以下

mousemoveごとの全page visual再生成と無制限cacheは追加していません。

## 手動未確認

- 実GUIでのpointer capture、OS DPI別の最終pixel、antialias、root seamの目視: `Not verified`
- 自動STA testでevent boundary、実Canvas hit、zoom handle、rotation、保存再読込、typed Canvas順は確認済みですが、実マウスpointer capture／OS DPI／最終pixelは目視成功扱いにしていません。`LinkedTextSingleObjectMoveLeavesBalloonGeometryUnchanged`は既存MojiPanelの単体移動・commit boundaryを通した数値確認であり、物理マウス移動そのものはNot verifiedです。

## 既知問題・逸脱・ADR候補

- 既知の機能不具合: なし
- 指示からの逸脱: なし
- schema／format version変更、新規package、一般group／multi-select、複数tail、auto layoutは実施していません。
- 新規ADR候補: なし。root parameterの具体mappingはADR-0007の実装判断として本報告へ記録しました。

## 後続影響・merge注意

- TASK-140は`TextLinkData`をそのまま利用し、自動改行／中央配置／自動縮小／padding layoutを追加できます。本taskのlink時位置保持と手動微調整を壊さないでください。
- TASK-150/160はsingle-tail slotとtyped composition内部順を前提にし、複数tailへ暗黙拡張しないでください。
- TASK-080の一般Z UI、TASK-230の一般group／multi-selectとは別概念です。`PageDocument.MoveBalloonComposition`を一般groupへ置換しないでください。
- TASK-120後の既存2テストは旧Z期待値を持っていたため、ADR-0007の`balloon -> linked text`順へ更新しています。

## Rollback

本branchのTASK-130 commitsを新しいrevert commitで逆順にrevertしてください。`push`、`merge`、`rebase`、履歴改変は実施していません。
