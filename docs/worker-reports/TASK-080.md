# TASK-080 実施報告

## 結果

TASK-080を実装完了。文字、フキダシ、付加記号で共通利用できる4つの重なり順操作とロック／ロック解除を追加し、既存のフキダシ typed composition の順序処理を共通オブジェクト順序処理へ一般化した。

- 対象ブランチ: `feature/TASK-080-zorder-lock`
- 対象worktree: `F:/github/MojiCollaTool-worktrees/TASK-080`
- 開始HEAD: `33c3f1d3c1575763484a379cb4fb3dde0fa4dcda`
- Round 1修正開始HEAD: `6c02fe2e719e8c101606984240d23e30f1774bf5`
- Round 2修正開始HEAD: `1840025e51b13dedec9b95c2a84b46077703bb82`
- Round 2実装コミット: `0bb2be75ece679e669e399e853401035d8896bae`
- Round 2受入testコミット: `631620b61cc2834e8b5dfa0ef7d34f08920adf39`
- 検証報告・台帳更新: 実装コミット後の文書コミット
- push / merge / rebase: 実施していない

## 要求・設計対応

- `REQ-ZORDER-001`: 最前面、前面、背面、最背面を `PageDocument.MoveObjectOrder` と共通UI／コンテキストメニューから実行可能にした。
- `REQ-LOCK-001`: `IsLocked` を共通コマンドで設定し、ロック中の直接選択、ドラッグ、リサイズ、テール操作、プロパティ変更、削除、重なり順変更を拒否した。
- `REQ-UI-JA-001`: ツールバーと右クリックメニューを日本語化し、ロック中の拒否メッセージも日本語にした。
- `REQ-UNDO-001`: 成功した順序変更・ロック変更は各1履歴エントリとし、no-op／拒否では履歴、dirty、通知を追加しない。
- ADR-0003/4: UUID、canonical `_objectOrder`、`ZIndex`、`IsLocked` と既存Undo/Redo方針を維持した。

## 司令役レビュー Round 1 対応

- `C080-01`: linked textと非detach付加記号を含むcomposition全memberをgeometry移動開始時に検査し、locked memberがあれば開始を拒否。gesture途中のlock変更はUpdate／Commitでbefore snapshotへcancelし、lock状態を保持して履歴・dirty・通知を増やさない。balloon単独のresize／tailは変更対象に応じて継続可能とした。
- `C080-02`: `MojiWindow`のtext／numeric／combo／check／全色／format読込／削除／付加記号CRUDを共通lock guardとcontrol enabled stateへ集約。開いたwindowへlock／unlockを即時反映し、`TryApplyLoadedFormat`をproduction helperとして分離した。
- `C080-03`: `BalloonCommands`のUpdate／Remove／SetTail／Link／Unlink、`AttachedSymbolCommands`のAdd／Update／Remove／Reanchor、PageEditorのlink／attached symbol／gesture経路で対象lockを検証し、拒否時は0 history・dirty不変とした。raw `PageDocument` mutationはserializer／capture用primitiveとして保持した。
- `C080-04`: `PageDocument.CanMoveObjectOrder`を共通availability計算として追加し、toolbar／context menuへoperation別の端判定とblock全member lock判定を同一適用。selected object自身のlock操作はcomposition内別memberのlockから独立させた。
- `C080-05`: TASK-080専用testを7件から12件へ拡張し、実XAML toolbar／context menu、3 object type、gesture atomicity、MojiWindow迂回、semantic command拒否、selection lifecycle／Dispose、saved dirty／0履歴境界をproduction経路で検証した。既存2.0〜2.2互換・2.3保存・横書き／縦書きsuiteも全suiteで再確認した。

## 順序・ロックの挙動

リンク済みフキダシ＋リンク済み文字＋非detach付加記号は1つの typed composition block として扱う。非リンク文字＋その非detach付加記号も1ブロック、detach付加記号は単独ブロックとした。操作時には各ブロック内のcanonical順序を保持し、ロックされたメンバーを含むブロックは原子性を保って拒否する。

右クリックではロック中のオブジェクトを選択可能にし、ロック解除だけを有効化した。ロック解除後は共通コマンド経由で通常操作へ戻る。既存のTASK-130 `MoveBalloonComposition` APIは互換ラッパーとして残し、内部は共通処理を利用する。

保存形式のバージョンやスキーマは変更していない。2.0〜2.2の読み込み互換性、2.3の順序・ロック状態の保存／再読込を確認した。

## 司令役＋sol_reviewer Round 2 対応

- `C080-06`: balloon、new text、old linked textの3者をlink差替え前に検査し、old linked textがlockedまたはmissingならUI／`BalloonCommands`双方でrelink、unlink、balloon removeを原子的に拒否する。解除後のrelink／remove成功も直接検証した。
- `C080-07`: toolbar／context lock変更の前に進行中のballoon、text、attached-symbol gestureをbefore snapshotへcancelしてから`CapturePage`する。途中geometryをPageDocument、live、Undo snapshotへ取り込まず、lock変更だけを1履歴として残すproduction testを追加した。
- `C080-08`: textと非detach attached symbolをgeometry move blockとして扱い、locked memberがある場合は開始拒否、途中lockではtext位置とsymbol表示をbeforeへ戻す。detach locked symbolは親text dragを妨げない。横書き／縦書き双方を確認した。
- `C080-09`: attached symbolのlock状態反映後、開いている親`MojiWindow`の選択を保持したまま編集controlだけを即時refreshし、handler guardと解除後成功を確認した。
- `C080-10`: text／balloon／attached symbolのcontext menu handlerを追跡可能なbindingへ変更し、remove、page rebind、unbind、DisposeでOpened／Click handlerを明示解除する。旧panel／menuを保持するnon-vacuous lifecycle testで再操作不能を確認した。
- `C080-11`: 3 object type×4 Z操作×toolbar/context menuの24 production経路でPageDocument順、live ZIndex、Canvas Z／children順、selectionを検証した。saved dirty、Undo/Redo、redo branch破棄、subscriber例外後のsemantic commit、2.3日本語path round-tripもTASK-080 testへ追加した。2.0〜2.2読込は既存versioned suiteで確認し、TASK-080専用fixtureとは表現していない。
- `C080-12`: `AttachedSymbolCommands.TryAdd(..., out Guid)`を追加し、成功IDとlocked／missing parent拒否を区別可能にした。既存`Add`は成功時のID契約を維持し、拒否時は`Guid.Empty`を返す。拒否は0 historyでdirtyを変えない。

## 検証

- SDK: `6.0.428`
- Debug build: 成功、警告0、エラー0
- Debug test: `232/232` 合格、失敗0
- Release build: 成功、警告0、エラー0
- Release no-build test: `232/232` 合格、失敗0
- TASK-080専用test: Debug／Release各 `20/20` 合格
- TASK-080性能（Debug）: mixed objects 100件、order p95 `1.186 ms`、100操作 `103.356 ms`
- TASK-080性能（Release）: mixed objects 100件、order p95 `1.435 ms`、100操作 `116.178 ms`
- 性能閾値: p95 50 ms未満、100操作 1000 ms未満
- `git diff --check`: 合格

自動テストではモデル、共通コマンド、typed composition／unlinked block、原子性、Undo/Redo、保存互換性、実toolbar／context menu event、selection／lifecycleを確認した。実OSのpointer入力、DPI、IME、最終ピクセル、OS依存のコンテキストメニュー表示・操作は`Not verified`である。

## 影響範囲・ロールバック

主な変更fileは `PageDocument.cs`、`PageEditorControl.xaml.cs`、`MojiPanel.cs`、`MojiWindow.xaml`／`.cs`、`BalloonCommands.cs`、`AttachedSymbolCommands.cs`、`TASK080ZOrderLockTests.cs`である。後続のTASK-200、TASK-210、TASK-230は共通コマンドと `ObjectOrderOperation` を利用できる。

ロールバック時はTASK-080の実装コミットと報告コミットを対象に戻し、保存形式2.3の既存読み書きとTASK-130互換APIを維持すること。
