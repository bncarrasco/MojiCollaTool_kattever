# TASK-140 自動テキストレイアウト実装報告

## 結果

TASK-140の自動レイアウトを、明示Applyによる一方向操作として実装しました。基本wrapは書記素単位で行い、`FullText`を書き換えません。CR/LF/CRLF/mixedの明示改行も保持します。高度な禁則、ハイフン分割、言語別line-break engineは対象外です。

未適用の単なる文字リンクは、保存再読込・BindPage・ページ切替・Undo/Redo後もwrapしません。`LayoutMode=Unapplied`を既存2.2フィールドのsentinelとして使用し、明示Apply後だけplanを再構築します。適用後の手動text/style編集とtext drag、balloon frame resizeでは未適用へ戻します。単なるballoon move・composition move・tail操作はframeの測定条件を変えないため、適用済みmode／planを維持します。

`FitTextToBalloon`はtext側のfont/position/visual planだけを変更し、`FitBalloonToText`はballoon側のposition/boundsだけを変更します。padding、最小文字サイズ、alignment、modeは同じ1履歴へcommitします。FitBalloonでは現在のframeが小さくても、有限・非負paddingならtext計測後の新しいboundsへ適用できます。

## C140-08〜15対応

- C140-08: 未適用link、明示Apply済みlink、適用後の手動編集・drag・resizeを区別し、lifecycleで意図しない自動wrapを行わないことを検証しました。
- C140-09: versioned XMLを`NewLineHandling.Entitize`で出力し、LF-only、CR-only、CRLF、mixed改行の文字列を完全往復します。C140-15でversioned形式を2.3へ更新しましたが、改行保持のXML境界は維持しています。
- C140-10: FitTextのframe内padding拒否とFitBalloonのtext＋padding計測を分離しました。24×24 frame、padding 20のFitBalloonを検証しました。
- C140-11: 適用途中に別balloonの不正linkで`CapturePage`を失敗させ、live model、PageDocument、Canvas、selection、z-order、AttachedSymbol、ComputedLayout、history、dirtyをdeep rollbackするテストを追加しました。
- C140-12: 実ComboBox選択と実Apply buttonのRaiseEvent、visual hierarchy/Z、redo branch、saved/dirty、両modeのPageEditor経路を検証しました。24 compositionを両modeで実測しました。
- C140-13: 本報告、CHANGELOG、ledger、verification matrixを日本語の受入記録へ整理しました。
- C140-14: balloon IDをtext IDとして誤って渡していたresize invalidationを修正し、8方向のframe resizeだけをUnapplied化・ComputedLayout clear対象としました。moveではmode／planを維持する回帰テストを追加しました。

## C140-15設計判断と実装

ADR-0005／ADR-0008でversion 2.3移行をAcceptedとしました。writerは`FormatVersion=2.3`、`MinimumReaderVersion=2.3`を出力し、2.3では`Unapplied`／`FitTextToBalloon`／`FitBalloonToText`の3 stateを正式保存します。

2.0〜2.2のreaderは、保存値がFitText、FitBalloon、missing、unknownのいずれでもlinkのlayout stateだけを`Unapplied`へ安全移行します。text content、geometry、style、padding、minimum、alignment、relationship、Z、AttachedSymbol、Canvas、assetは変更しません。旧archiveは読込だけでは書き換えず、次回の明示保存時に2.3として出力します。2.4以降、unknown major、future minimum readerは拒否します。

## Git情報

- Task: `TASK-140`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-140`
- Branch: `feature/TASK-140-auto-text-layout`
- Functional base / 開始HEAD: `6adc3c9affd1cc72fec3dfc7633e8794bdb05b3c`
- 前回レビュー修正commit: `faf756cec414bb7fcf65b3a300778c7a62e52065`
- 今回の実装・テストcommit: `467a62c278ed38d70a69c53f1be9d902b96dfe0c`
- 未知のLayoutModeを暗黙適用しない安全弁commit: `5a74db3a8c38d7b2b900d11109942c4460d7e3de`
- C140-14 resize修正・回帰test commit: `a54177d527f79e0ef06556c1ac81c39a5a9d5044`
- C140-15 version 2.3 migration実装・互換test commit: `44c45dd5b9ef3bb2cf13dc54280ca182934bc6d1`
- C140-14文書commit（今回の最終記録直前のfull hash）: `a366e79125736eb09f7c34660801a39110cd25de`
- SDK: `C:\Users\user\.dotnet\dotnet.exe --version` = `6.0.428`
- push / merge / rebase: 実施していません

## 主な変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/BalloonData.cs`
- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `MojiCollaTool/MojiCollaTool/Layout/TextLayoutService.cs`
- `MojiCollaTool/MojiCollaTool/MojiPanel.cs`
- `MojiCollaTool/MojiCollaTool/MojiWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml`
- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `tests/MojiCollaTool.Tests/TASK140AutoTextLayoutTests.cs`

## 検証

- Debug build: 成功、警告0、エラー0
- Debug全test: 212/212成功、失敗0
- Release build: 成功、警告0、エラー0
- Release全test: 212/212成功、失敗0
- `git diff --check`: 成功
- Release production PageEditor実測: p95 36.124 ms、FitText 24件 755.202 ms、FitBalloon 24件 715.984 ms
- Debug production PageEditor実測: p95 38.318 ms、FitText 24件 764.291 ms、FitBalloon 24件 751.505 ms
- Release閾値: single p95 100 ms未満、各24件batch 1,000 ms未満。Debug安全閾値は3,000 ms未満です。
- 自動検証範囲: 書記素wrap、4改行variant、2方向・2フォント、未適用lifecycle、2.0／2.1／2.2 migration、2.3三状態round-trip、2.4／future gate、2.2読込→明示Apply→2.3保存、versioned round-trip、padding、UI拒否、deep rollback、subscriber例外、両mode invariants、no-op、tail、AttachedSymbol、redo branch、visual hierarchy、実UI、production performance、resize／move gesture policy。
- C140-15主要test: `Version23PersistsAllThreeLayoutStatesAndManifestVersions`、`Version22LinksMigrateOnlyLayoutStateToUnappliedWithoutImplicitPlan`、`Version22ReadExplicitApplyThenVersion23SavePreservesLayoutAndCompositionState`、`WriterUsesVersion23AndReaderAcceptsVersion20Archive`、`Version21ArchiveRemainsReadable`、`FutureMinorVersionIsRejected`。
- WPFの実マウスpointer、OS DPI、IME、最終pixel目視: 未検証

## 既知の境界

- text edit、text drag、balloon frame resize後は自動再適用せず、ユーザーが再度Applyします。balloon move、composition move、tail操作は適用済みplanを維持します。
- 2.0〜2.2の旧layout stateは明示適用済みとは判定せずUnappliedへ移行するため、旧projectを次回保存するまで自動layoutは再適用されません。
- フォント生成に失敗した場合は有限な近似measureへfallbackします。measure cache容量は2048です。
- 高度禁則、hyphenation、blur、continuous reactive layoutはTASK-140対象外です。

## Rollback

rollbackが必要な場合は、このbranch上でrevert commitを作成してください。push、merge、rebaseは行いません。
