# Changelog

## Unreleased

### TASK-190B

- アプリのタイトル、About画面、Assembly/package metadataを`MojiCollaTool 勝手版`に統一しました。
- About画面に非公式フォークであること、公式版・原作者とは無関係であること、原作者へ問い合わせない旨を表示します。

### TASK-030

- 複数プロジェクトと複数ページを二段タブで切り替え、ページ操作・dirty表示・安全な個別closeを行えるworkspace shellを追加しました。
- ページ画像をsession/page単位の一時asset storeで隔離し、versioned/legacyの読込・保存とページ複製に接続しました。

### TASK-041

- 旧形式のroot-entry `.mctzip` を自動検出し、1ページのプロジェクトとして読み込むlegacy import readerを追加しました。読込結果には移行warningを付与し、画像をasset sinkへ復元できます。

### Added

- バージョン化された `.mctzip` 複数ページ形式の読み書きを追加しました。manifest、ページ単位XML、ページごとの背景画像、画像metadataとの整合性・decode検証、形式バージョン検証、原子的な保存、任意の1世代バックアップに対応します。

### Changed

- READMEを「MojiCollaTool 勝手版」として明示し、非公式フォークであること、問い合わせ先の注意、現時点の配布状況を記載しました。ColorSelector由来コードのApache-2.0 noticeも追加しました。

### Fixed

- 旧形式 `.mctzip` の保存・読込を一時領域で検証してから反映するようにし、保存途中の失敗や破損アーカイブで既存データを失わないようにしました。
- 保存時にページごとの画像entryを明示コピーし、画像の重複拡張子・CanvasData不整合・復号失敗を検出するようにしました。

### Compatibility

### Known issues

- legacy形式の検出・移行は後続のTASK-041で実装します。
