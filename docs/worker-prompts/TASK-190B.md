# TASK-190B ワーカー指示書

## 役割と作業場所

- Branch: `feature/TASK-190B-app-branding`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-190B`
- Base: `develop`（開始時のコミットを記録する）

## 目的

アプリ本体と配布時 metadata に、`MojiCollaTool 勝手版` が非公式フォークであることを明示する。公式版・原作者と誤認される表示や、原作者への問い合わせ導線を追加しない。

## Scope

- MainWindowのタイトルとAbout導線
- About画面の非公式フォーク、原作者非関係、問い合わせ先注意、LICENSE/第三者notice案内
- Assembly/package metadata
- brandingの自動テスト、CHANGELOG、worker report、verification matrix、implementation ledger

## Scope外

- 内部namespace、プロジェクト形式のProduct識別子、root LICENSE、第三者notice本文
- installerの作成、Release、push、merge、原作者への連絡
- TASK-200の既存UI日本語化

## 完了条件

- title/About/metadataの表示が`MojiCollaTool 勝手版`で統一されている。
- Aboutに非公式フォーク、公式版および原作者との無関係、原作者へ問い合わせない旨がある。
- LICENSEおよび`THIRD-PARTY-NOTICES.md`の存在と案内を維持する。
- Debug/Release build、test、manual inspectの結果を報告する。環境不足時は`Not verified`と記録する。
