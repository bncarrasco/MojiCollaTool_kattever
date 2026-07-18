# TASK-041 ワーカー指示書

## 役割と作業場所

あなたは旧形式読込・移行担当です。公式互換export、MainWindow、タブUI、他機能を実装しません。

- Branch: `feature/TASK-041-legacy-project-import`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-041`
- Functional base: `6cc9b431e738dea7b6dfd7e56b008edaca8cc88e`
- 開始時HEAD: 正式worktree作成時に司令役がkickoff指示へexact hashを記載する。functional base以後は本指示書とworktree運用文書の追加だけを許可する。
- 統合済み依存: TASK-005、TASK-010、TASK-020、TASK-025、TASK-040
- 最初に `AGENTS.md`、`docs/worker-prompts/README.md`、本指示書、ADR-0005、project-format互換性仕様を読む。

既存の同名directoryが `git worktree list --porcelain` に存在しない場合、それは未管理コピーであり正式な作業場所ではない。変更を続けず司令役へ報告する。正式worktree作成後、T041固有差分だけを再適用し、未管理コピー全体を上書き元にしない。

## 目的・要件

- REQ-COMPAT-001
- ADR-0005
- 旧root-entry `.mctzip`を安全に検出し、1ページの `ProjectDocument`へ損失なく移行する。
- manifest形式はversioned readerへ委譲し、壊れたmanifestをlegacyへfallbackしない。

## Scope

- legacy/versioned形式判定
- common reader resultと日本語の移行warning
- 既存の安全なlegacy staging readerと`LegacyProjectDataAdapter`の再利用
- 旧形式画像0/1/2枚のProjectSession単位asset復元
- DTD、entry件数、破損形式、画像不整合の安全な拒否
- golden fixture、Unicode、日本語path、縦書き／横書き、未知XML fieldの期待固定

## Scope外

- 公式版互換export
- MainWindowへのwarning表示
- 新形式への自動保存
- プロジェクト／ページタブUI
- TASK-041と無関係なリファクタリング

## 前回レビューからの必須修正

- `ProjectFormatDetector`でentry名を配列化する前に件数上限を検査し、共通reader入口で過剰entryを拒否する。
- 画像0枚、画像1枚＋非batch sink、画像2枚＋batch sinkを個別に検証する。
- legacy fixtureの複数MojiDataについて、件数、ID、縦書き／横書き、Unicode本文を具体値で検証する。
- CanvasData／MojiDataの未知XML fieldを安全に無視する期待をテストする。
- legacy rootと壊れた`manifest.xml`が共存してもversioned形式として拒否され、legacyへfallbackしないことをテストする。
- entry上限超過を形式判定段階で拒否するテストを追加する。

## 変更領域

- 変更可: common/legacy reader、必要最小限の`DataIO` adapter、T041 tests、T041報告書、台帳のTASK-041行、ユーザー向け変更がある場合のCHANGELOG。
- 変更禁止: MainWindow、PageEditorControl、Workspace、T040 serializer/writerの無関係な変更、他taskの報告書・台帳行。

## 受入・検証

- Debug build、Release build、全test、`git diff --check`が成功する。
- baseにある52テストを維持し、T041追加テストを合わせた総数を報告する。
- 日本語path、Unicode、縦書き／横書き、画像0/1/2枚、未知field、DTD、壊れたmanifest、entry上限を検証する。
- UI手動確認はScope外として理由を記録し、未実施を成功扱いしない。

## 完了処理

- `docs/worker-reports/TASK-041.md`を作成する。
- `docs/planning/implementation-ledger.md`は既存TASK-041行だけを更新し、別表を追加しない。
- 英語commit messageで自branchへcommitする。push/merge/rebaseしない。
- 報告書へ開始時Git確認、Base/Result commit、変更file、build/test、互換性、既知risk、後続TASK-030、rollback方法を記録する。
