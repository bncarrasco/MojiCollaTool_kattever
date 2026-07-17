# Changelog

## Unreleased

### Added

- バージョン化された `.mctzip` 複数ページ形式の読み書きを追加しました。manifest とページ単位 XML、形式バージョン検証、原子的な保存、任意の1世代バックアップに対応します。

### Changed

- READMEを「MojiCollaTool 勝手版」として明示し、非公式フォークであること、問い合わせ先の注意、現時点の配布状況を記載しました。ColorSelector由来コードのApache-2.0 noticeも追加しました。

### Fixed

- 旧形式 `.mctzip` の保存・読込を一時領域で検証してから反映するようにし、保存途中の失敗や破損アーカイブで既存データを失わないようにしました。
- 保存時に現在の画像entryを明示コピーし、画像の重複拡張子・CanvasData不整合・復号失敗を検出するようにしました。

### Compatibility

### Known issues

- 計画段階であり、統合指示に記載された新機能はまだ実装されていません。
