# TASK-080 実施報告

## Result

TASK-080「重なり順とロックUI」は完了した。文字、フキダシ、付加記号へ共通の4方向Z-order操作とロック／ロック解除を実装し、C080-01〜17のレビュー指摘を反映した。保存形式・外部依存・SDKは変更していない。

## 開始時Git確認

- Branch: `feature/TASK-080-zorder-lock`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-080`
- Functional base: `33c3f1d3c1575763484a379cb4fb3dde0fa4dcda`
- 司令役介入後の仕上げ開始HEAD: `c10942c279d6c28f1b9eb18a6f081676736c5942`
- `git rev-parse --show-toplevel`、branch、HEAD、clean status、正式worktree登録は指示値と一致した。
- SDK: `C:/Users/user/.dotnet/dotnet.exe`、version `6.0.428`
- push / merge / rebase: 未実施

## 対応要件・設計

- `REQ-ZORDER-001`: 「最前面へ」「前面へ」「背面へ」「最背面へ」をtext／balloon／attached symbolの共通操作にした。
- `REQ-LOCK-001`: 直接操作だけでなく、typed composition、relationship変更、親文字移動、削除、semantic commandの間接変更もlock境界で拒否する。
- `REQ-UI-JA-001`: toolbar、context menu、状態表示、拒否理由を日本語にした。
- `REQ-UNDO-001`: 成功操作は1履歴、coalesce対象は1履歴へ統合し、no-op／拒否／validation失敗／callback例外は履歴とdirtyを変えない。
- ADR-0003／0004: UUID、canonical object order、正規化ZIndex、ProjectSession historyを維持した。ADR追加候補はない。

## 実装概要

- linked balloon＋text＋非detached symbol、およびunlinked text＋非detached symbolを決定的なtyped blockとして移動する。detached symbolは単独blockとする。
- model順、live ZIndex、`Canvas.GetZIndex`、Canvas typed children順を同期し、block内にlocked memberがあれば原子的に拒否する。
- drag／resize／tail／property／MojiWindow／link／unlink／relink／remove／reanchor／generic Updateをlock-awareにし、gesture途中のlockはcapture前にbefore snapshotへ戻す。
- text削除はlocked linked balloonまたはlocked non-detached symbolを個別にpreflightし、関係を部分変更しない。
- generic Updateではrelationship fieldの変更を拒否し、専用commandだけを許可する。
- `AttachedSymbolCommands.TryAdd`は成功IDとlocked／missing parent拒否を区別する。
- context menuのOpened／Click handlerを所有bindingとして管理し、remove／rebind／unbind／Disposeで明示解除する。
- 検査済みUpdate candidateはcallbackを再実行せずcommitする。callbackは厳密に1回だけ評価し、確定candidateとnested `Tail`／`TextLink`はdeep cloneして呼出側aliasから隔離する。

## レビューとエスカレーション

- 通常のimplementer／reviewer loop: 4巡。Round 4でC080-17が残りhard capへ到達した。
- 司令役の直接介入: 1回。commit `c10942c279d6c28f1b9eb18a6f081676736c5942`で検査済みcandidateの単回commitを実装した。
- 介入後read-only review: 1巡。BLOCKER 0、MAJOR 0、source findingsなし。
- 介入後implementer仕上げ: test commit `d5a1b03c055427eea2fb2677bdc4fb48233b00bd`。sourceは変更していない。
- 主な先行修正commit: `0bb2be75ece679e669e399e853401035d8896bae`、`631620b61cc2834e8b5dfa0ef7d34f08920adf39`、`0a172a01a0696d244424f214a57d1f8217a424f3`。

## 自動検証

- Debug build: 成功、警告0、エラー0
- Debug全test: `238/238` 合格、失敗0、skip 0
- Release build: 成功、警告0、エラー0
- Release `--no-build`全test: `238/238` 合格、失敗0、skip 0
- TASK-080専用test: Debug／Release各 `26/26` 合格
- `git diff --check`: 合格
- 100 mixed objects性能（直近記録）: Debug p95 `1.186 ms`／100操作 `103.356 ms`、Release p95 `1.435 ms`／100操作 `116.178 ms`。閾値はp95 50 ms未満、100操作1,000 ms未満。

直接確認した主な境界は、3 object type×4 Z操作×toolbar/context経路、operation別順序、model/live/Canvas/selection、saved dirty、Undo/Redo、redo branch、coalesce、subscriber／callback／validation例外、candidate deep-clone alias隔離、locked relationship、page lifecycle、2.3日本語path round-trip、横書き／縦書きである。2.0〜2.2読込は既存versioned suiteによる間接確認であり、TASK-080専用fixtureとは表現しない。

## 手動未検証・既知事項

- 実OS pointer、DPI、IME、最終pixel、OS-native context menuの表示・操作は`Not verified`。
- locked objectのright-click選択はselection helperとcontext actionを組み合わせた間接確認であり、production routed right-click eventそのものは`Not verified`。
- routed left-clickは日本語status、選択結果、mouse capture拒否まで自動確認した。
- 新しい外部依存、保存schema変更、既知の未解決BLOCKER／MAJORはない。

## 変更file・後続影響

主なproduction変更は`PageDocument.cs`、`PageEditorControl.xaml.cs`、`MojiPanel.cs`、`MojiWindow.xaml.cs`、`ObjectCommands.cs`、`BalloonCommands.cs`、`AttachedSymbolCommands.cs`。自動testは`TASK080ZOrderLockTests.cs`、文書は本報告、implementation ledger、verification matrix、CHANGELOGを更新した。

TASK-200／210／230は共通ObjectCommandsとlock／order availabilityを利用できる。TASK-080統合時は同じUI領域を変更するtaskと直列化する。

## ロールバック

介入後だけを戻す場合は文書commit、`d5a1b03c055427eea2fb2677bdc4fb48233b00bd`、`c10942c279d6c28f1b9eb18a6f081676736c5942`を逆順にrevertする。TASK-080全体を戻す場合はfunctional base `33c3f1d3c1575763484a379cb4fb3dde0fa4dcda`以後のTASK-080 merge対象commitを逆順にrevertし、既存format 2.3とTASK-130／140の契約を残す。
