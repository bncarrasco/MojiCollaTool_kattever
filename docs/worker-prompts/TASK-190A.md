# TASK-190A ワーカー指示書

## 役割と作業場所

あなたは非公式フォーク文書・第三者帰属担当です。product codeやUIを変更しません。

- Branch: `feature/TASK-190A-fork-documentation`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-190A`
- Base: tag `plan-base-20260717`。開始時に `git rev-parse HEAD` とtagのhash一致を記録する。
- 最初に `AGENTS.md`、本指示書、requirements、current-state-review、LICENSE、README、`Document/ColorPicker.md`、`ColorSelector/*`を読む。

## 背景・目的・要件

現在READMEは公式release/wikiへ案内し、勝手版/非公式/問い合わせ先注意がない。ColorSelectorはWpfColorPicker由来と見られ、既存メモはApache-2.0を示すが正式noticeがない。REQ-FORK-001、REQ-NFR-DEPS-001を文書範囲で満たす。

## Scope

- READMEを「MojiCollaTool 勝手版」として更新する。
- kuramiya氏の非公式fork、原作者/公式版と無関係、勝手版の問い合わせを原作者へ送らない、元copyright/MIT維持を明記する。
- download/wiki linkを、現時点で存在しない勝手版releaseへ誤誘導しない表現にする。
- ColorSelectorの由来を一次source、commit history、実code比較で確認する。
- 必要な場合だけ `THIRD-PARTY-NOTICES.md`等を追加し、copyright/license text要件を満たす。
- CHANGELOGへuser-facingな文書変更を記録する。

## Scope外

- MainWindow title、About dialog、Assembly metadata、C#/XAML、licenseの勝手な変更、依存削除/置換、release/push、公式作者への連絡。

## 変更領域と競合回避

- 変更可: README、new third-party notice、CHANGELOG、自task report/ledger/matrix行。
- 変更不可: product C#/XAML、solution/csproj、root LICENSEの元copyright削除、他task docs全面変更。
- TASK-000はsolution/testsを変更するため、同領域を触れない。

## UI・保存・互換性

- product UI/formatへ影響なし。日本語文書を使用する。
- license由来を確定できない場合、推測で断定せず、candidate source、差異、追加確認事項をreportへ記録する。

## 受け入れ・検証

- READMEだけで非公式性、問い合わせ注意、元作者/license維持が理解できる。
- official releaseを勝手版最新版として案内しない。
- third-party noticeは確認できた事実とlicense条件に基づく。
- link確認、Markdown表示確認、`git diff --check`を実施する。
- product非変更のためbuild/testはN/Aとできるが、N/A理由を明記する。

## 完了処理

- `docs/worker-reports/TASK-190A.md`を作成し、自分のledger/matrix行だけを更新する。
- meaningfulな英語commit messageで自branchへcommitする。push/merge/rebaseしない。
- 最終報告: Result、commit hash、調査source、変更file、license判断、未確定事項、TASK-190Bへの注意、rollback方法。
