# TASK-080 実施報告

## 結果

TASK-080を実装完了。文字、フキダシ、付加記号で共通利用できる4つの重なり順操作とロック／ロック解除を追加し、既存のフキダシ typed composition の順序処理を共通オブジェクト順序処理へ一般化した。

- 対象ブランチ: `feature/TASK-080-zorder-lock`
- 対象worktree: `F:/github/MojiCollaTool-worktrees/TASK-080`
- 開始HEAD: `33c3f1d3c1575763484a379cb4fb3dde0fa4dcda`
- 実装コミット: `TBD`
- 最終報告コミット: `TBD`
- push / merge / rebase: 実施していない

## 要求・設計対応

- `REQ-ZORDER-001`: 最前面、前面、背面、最背面を `PageDocument.MoveObjectOrder` と共通UI／コンテキストメニューから実行可能にした。
- `REQ-LOCK-001`: `IsLocked` を共通コマンドで設定し、ロック中の直接選択、ドラッグ、リサイズ、テール操作、プロパティ変更、削除、重なり順変更を拒否した。
- `REQ-UI-JA-001`: ツールバーと右クリックメニューを日本語化し、ロック中の拒否メッセージも日本語にした。
- `REQ-UNDO-001`: 成功した順序変更・ロック変更は各1履歴エントリとし、no-op／拒否では履歴、dirty、通知を追加しない。
- ADR-0003/4: UUID、canonical `_objectOrder`、`ZIndex`、`IsLocked` と既存Undo/Redo方針を維持した。

## 順序・ロックの挙動

リンク済みフキダシ＋リンク済み文字＋非detach付加記号は1つの typed composition block として扱う。非リンク文字＋その非detach付加記号も1ブロック、detach付加記号は単独ブロックとした。操作時には各ブロック内のcanonical順序を保持し、ロックされたメンバーを含むブロックは原子性を保って拒否する。

右クリックではロック中のオブジェクトを選択可能にし、ロック解除だけを有効化した。ロック解除後は共通コマンド経由で通常操作へ戻る。既存のTASK-130 `MoveBalloonComposition` APIは互換ラッパーとして残し、内部は共通処理を利用する。

保存形式のバージョンやスキーマは変更していない。2.0〜2.2の読み込み互換性、2.3の順序・ロック状態の保存／再読込を確認した。

## 検証

- SDK: `6.0.428`
- Debug build: 成功、警告0、エラー0
- Debug test: `219/219` 合格、失敗0
- Release build: 成功、警告0、エラー0
- Release no-build test: `219/219` 合格、失敗0
- TASK-080性能（Debug）: mixed objects 100件、order p95 `1.987 ms`、100操作 `166.141 ms`
- TASK-080性能（Release）: mixed objects 100件、order p95 `1.592 ms`、100操作 `128.023 ms`
- 性能閾値: p95 50 ms未満、100操作 1000 ms未満
- `git diff --check`: 合格

自動テストではモデル、共通コマンド、typed composition／unlinked block、原子性、Undo/Redo、保存互換性、ツールバー、右クリックメニューの構造を確認した。実OSのマウス入力、DPI、IME、最終ピクセル、OS依存のコンテキストメニュー操作は未検証である。

## 影響範囲・ロールバック

主な影響範囲は `PageDocument`、`PageEditorControl`、文字／付加記号／フキダシのロック保護、共通オブジェクトコマンド、TASK-080テストおよび検証ドキュメントである。後続のTASK-200、TASK-210、TASK-230は共通コマンドと `ObjectOrderOperation` を利用できる。

ロールバック時はTASK-080の実装コミットと報告コミットを対象に戻し、保存形式2.3の既存読み書きとTASK-130互換APIを維持すること。
