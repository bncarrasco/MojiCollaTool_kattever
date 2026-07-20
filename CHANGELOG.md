# Changelog

## Unreleased

### TASK-140

- フキダシとリンク文字の「枠に文字を合わせる」「文字に枠を合わせる」を明示適用できる自動レイアウトを追加しました。
- 基本wrapは書記素単位で行い、CRLF/LFなどの明示改行とFullTextを変更せず、縦書き・横書き・Start/Center/Endに対応します。高度な禁則・ハイフン分割は対象外です。
- 最小文字サイズ、はみ出し警告、有限容量のフォント測定cache、1回のUndo/Redo履歴としてのatomic適用を追加しました。

### TASK-130

- フキダシのしっぽを追加・削除し、先端・付け根・幅の3ハンドルで編集できるようにしました。
- 同じページの文字をフキダシへ一対一でリンクし、フキダシ移動時にしっぽと文字を一体移動できるようにしました。
- フキダシ、リンク文字、その付加記号を一つの構成単位として、前面・背面へ移動できる日本語UIを追加しました。
- しっぽ編集、文字リンク、一体移動、重なり順の変更をUndo/Redoと保存再読込へ対応しました。
- link直後に文書順・実Canvas順を同期し、既存format 2.2の重複文字linkはcanonical順で後続をunlinkする互換移行を追加しました。実UIのlink／unlink・Z操作・履歴・page切替の受入確認を拡張しました。
- link／unlinkの同期失敗は文書・visual・履歴を原子的に復元し、semantic commit後の通知subscriber例外は上位処理へ伝播するようにしました。内部例外の詳細は日本語statusへ表示しません。

### TASK-100

- 本文をUnicode書記素単位で描画し、親文字へ `!`、`?`、`!?` などの付加記号をem基準の位置・倍率・回転で追加・編集できるようにしました。
- 付加記号のフォント・色・縁取り継承、システムフォントfallback、縦書き／横書き追従、親文字編集時の再アンカー、保存・Undo/Redoを追加しました。
- 付加記号のドラッグは操作完了時に1件の履歴として記録し、capture lossでは編集前へ戻します。
- detached付加記号を可視visualと分離して保持し、親削除・ページ切替・保存再読込でも失わないようにしました。追加時の検証とCaptureの試行検証を原子的に行い、不正入力でUI／文書／履歴が変化しないようにしました。
- 濁点・半濁点・装飾継承・文字間隔設定、実スクロール領域、非表示visual抑止、完全grapheme文字列pool、gesture単位のdrag履歴を追加しました。
- 受入テストを20件へ拡張し、ParentId別の実UI一覧CRUD、追加直後canonical ZIndex、親回転local offset／drag、同一行再クリック、Expander折りたたみ、重なる座標のmixed実描画Z-orderと保存再読込、相互排他的selection、入力拒否、2種類の認識済みフォントの横／縦配置、UI経路の履歴・dirty状態・redo branch、24個の付加記号refresh／drag性能を自動検証しました。

### TASK-120

- 4種のフキダシgeometryと上限付きLRU cache、未知shapeの矩形fallbackを追加。
- PageEditorControlに日本語のshape選択・追加、選択表示、移動、8方向resize、最小サイズ制限、zoom対応handleを追加。
- drag/resizeはpreview中に履歴を増やさず、pointer-upで1件だけcommitし、capture lossでは元状態へ戻す。

### TASK-050

- 現在のページまたはactive projectの全ページを、ページ順・安全な連番ファイル名でPNG/JPEGへ一括出力できるようにしました。
- 既存ファイルの上書きを防ぎ、ページ単位の失敗を継続して成功数・失敗ページを日本語で表示します。JPEGはCanvasColorを維持したまま透明部分だけ白背景へ合成し、PNGは透明背景を保持します。

### TASK-070

- Undo/Redo、保存revisionによるdirty表示、ページ・画像assetのatomic復元、連続入力のcoalesceを追加しました。
- `Ctrl+Z`、`Ctrl+Y`、`Ctrl+Shift+Z`で現在のプロジェクトだけを操作できるようにしました。

### TASK-110

- 楕円・角丸四角・四角・モノローグのフキダシモデル、塗り・枠線・位置・回転・ロック・表示状態を追加しました。
- 単一しっぽ、文字リンク、ページ内の文字／フキダシZ順、ID remap付き複製、削除時のリンク解除を保存可能にしました。
- versioned `.mctzip` のpage.xmlへフキダシを追加し、未知shapeの安全な読込、Undo/Redo対応のモデルコマンドを追加しました。
- 形式versionを2.1へ更新し、2.0形式の読込互換、混在Z順のSet／履歴復元、フキダシを含む履歴メモリ見積りを追加しました。

### TASK-190B

- アプリのタイトル、About画面、Assembly/package metadataを`MojiCollaTool 勝手版`に統一しました。
- About画面に非公式フォークであること、公式版・原作者とは無関係であること、原作者へ問い合わせない旨を表示します。

### TASK-030

- 複数プロジェクトと複数ページを二段タブで切り替え、ページ操作・dirty表示・安全な個別closeを行えるworkspace shellを追加しました。
- ページ画像をsession/page単位の一時asset storeで隔離し、versioned/legacyの読込・保存とページ複製に接続しました。

### TASK-041

- 旧形式のroot-entry `.mctzip` を自動検出し、1ページのプロジェクトとして読み込むlegacy import readerを追加しました。読込結果には移行warningを付与し、画像をasset sinkへ復元できます。

### TASK-060

- オブジェクトへUUID、種別、ZIndex、ロック、表示、親/グループ参照を追加し、ページ内描画順を保存可能なcanonical orderへ正規化しました。
- 旧int IDを互換保持しつつ、ページ・プロジェクト内の重複UUIDを検出し、ページ複製時は新しいUUIDへ切り替えるようにしました。

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
