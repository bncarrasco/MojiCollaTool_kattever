# TASK-140 自動テキストレイアウト実装報告

## 結果

TASK-140の自動レイアウトを、明示Applyによる一方向操作として実装しました。基本wrapは書記素単位で行い、`FullText`を書き換えません。CR/LF/CRLF/mixedの明示改行も保持します。高度な禁則、ハイフン分割、言語別line-break engineは対象外です。

未適用の単なる文字リンクは、保存再読込・BindPage・ページ切替・Undo/Redo後もwrapしません。`LayoutMode=Unapplied`を既存2.2フィールドのsentinelとして使用し、明示Apply後だけplanを再構築します。適用後の手動text/style編集とtext drag、balloon frame resizeでは未適用へ戻します。単なるballoon move・composition move・tail操作はframeの測定条件を変えないため、適用済みmode／planを維持します。

`FitTextToBalloon`はtext側のfont/position/visual planだけを変更し、`FitBalloonToText`はballoon側のposition/boundsだけを変更します。padding、最小文字サイズ、alignment、modeは同じ1履歴へcommitします。FitBalloonでは現在のframeが小さくても、有限・非負paddingならtext計測後の新しいboundsへ適用できます。

## C140-08〜13対応

- C140-08: 未適用link、明示Apply済みlink、適用後の手動編集・drag・resizeを区別し、lifecycleで意図しない自動wrapを行わないことを検証しました。
- C140-09: versioned XMLを`NewLineHandling.Entitize`で出力し、LF-only、CR-only、CRLF、mixed改行の文字列を完全往復します。version/schemaは変更していません。
- C140-10: FitTextのframe内padding拒否とFitBalloonのtext＋padding計測を分離しました。24×24 frame、padding 20のFitBalloonを検証しました。
- C140-11: 適用途中に別balloonの不正linkで`CapturePage`を失敗させ、live model、PageDocument、Canvas、selection、z-order、AttachedSymbol、ComputedLayout、history、dirtyをdeep rollbackするテストを追加しました。
- C140-12: 実ComboBox選択と実Apply buttonのRaiseEvent、visual hierarchy/Z、redo branch、saved/dirty、両modeのPageEditor経路を検証しました。24 compositionを両modeで実測しました。
- C140-13: 本報告、CHANGELOG、ledger、verification matrixを日本語の受入記録へ整理しました。
- C140-14: balloon IDをtext IDとして誤って渡していたresize invalidationを修正し、8方向のframe resizeだけをUnapplied化・ComputedLayout clear対象としました。moveではmode／planを維持する回帰テストを追加しました。

## C140-15設計判断（未実装・承認待ち）

TASK-130時代のversion 2.2には、auto layout未実装時の既定値として`FitTextToBalloon`が保存され得ます。同じ2.2のfieldだけでは、旧writerの値とTASK-140の明示Applyを決定的に識別できません。従って、現時点で2.2を一律変換したりgeometry推測を行ったりせず、旧projectを開いた際の表示／exportが変わり得る互換性リスクを既知問題として残します。

ADR候補はversion 2.3への移行です。2.0〜2.2は全linkを`Unapplied`へ安全移行し、2.3から`Unapplied`／`FitTextToBalloon`／`FitBalloonToText`を正式保存します。採用承認まではreader／writer、future version gate、2.2 fixture migration、2.3 round-tripを変更しません。

## Git情報

- Task: `TASK-140`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-140`
- Branch: `feature/TASK-140-auto-text-layout`
- Functional base / 開始HEAD: `6adc3c9affd1cc72fec3dfc7633e8794bdb05b3c`
- 前回レビュー修正commit: `faf756cec414bb7fcf65b3a300778c7a62e52065`
- 今回の実装・テストcommit: `467a62c278ed38d70a69c53f1be9d902b96dfe0c`
- 未知のLayoutModeを暗黙適用しない安全弁commit: `5a74db3a8c38d7b2b900d11109942c4460d7e3de`
- C140-14 resize修正・回帰test commit: `a54177d527f79e0ef06556c1ac81c39a5a9d5044`
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
- Debug全test: 209/209成功、失敗0
- Release build: 成功、警告0、エラー0
- Release全test: 209/209成功、失敗0
- `git diff --check`: 成功
- Release production PageEditor実測: p95 36.124 ms、FitText 24件 755.202 ms、FitBalloon 24件 715.984 ms
- Debug production PageEditor実測: p95 38.318 ms、FitText 24件 764.291 ms、FitBalloon 24件 751.505 ms
- Release閾値: single p95 100 ms未満、各24件batch 1,000 ms未満。Debug安全閾値は3,000 ms未満です。
- 自動検証範囲: 書記素wrap、4改行variant、2方向・2フォント、未適用lifecycle、versioned round-trip、padding、UI拒否、deep rollback、subscriber例外、両mode invariants、no-op、tail、AttachedSymbol、redo branch、visual hierarchy、実UI、production performance、resize／move gesture policy
- WPFの実マウスpointer、OS DPI、IME、最終pixel目視: 未検証

## 既知の境界

- text edit、text drag、balloon frame resize後は自動再適用せず、ユーザーが再度Applyします。balloon move、composition move、tail操作は適用済みplanを維持します。
- C140-15のversion 2.3移行は承認待ちで未実装です。2.2旧projectの既定FitTextと明示Applyを識別できない互換性リスクがあります。
- フォント生成に失敗した場合は有限な近似measureへfallbackします。measure cache容量は2048です。
- 高度禁則、hyphenation、blur、continuous reactive layoutはTASK-140対象外です。

## Rollback

rollbackが必要な場合は、このbranch上でrevert commitを作成してください。push、merge、rebaseは行いません。
