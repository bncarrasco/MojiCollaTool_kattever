# アーキテクチャ計画

Status: Proposed

原則: 段階的移行、既存WPF描画の再利用、文書状態と表示状態の分離

## 1. 目標構造

```text
MainWindow (workspace shell)
├─ ApplicationWorkspace
│  └─ ProjectSession [0..n]
│     ├─ ProjectDocument
│     │  └─ PageDocument [1..n]
│     │     ├─ CanvasDocument
│     │     └─ EditorObjectData [0..n]
│     ├─ ProjectHistory / PageHistory
│     └─ FilePath, SavedRevision, ActivePageId
└─ ProjectTab
   └─ PageTab
      └─ PageEditorControl
```

上段をプロジェクトタブ、下段をページタブとする。Chrome型の単一タブバーの色付き折りたたみgroupは初期対象外とし、文書階層をUI上でも明確にする。

## 2. 責務

### ApplicationWorkspace

- 開いている `ProjectSession`、active project、同一path重複防止、application終了時の未保存一覧を管理する。
- WPF Windowそのものを文書データへ保持しない。

### ProjectSession

- `ProjectDocument`、FilePath、SavedRevision、active page、履歴service、project-level commandを所有する。
- tabを閉じると編集Window、cache、clipboard参照を解放する。
- 読み込みは一時sessionへ完了してからworkspaceへ追加し、失敗時に既存sessionへ影響させない。

### ProjectDocument / PageDocument

- `ProjectDocument`: FormatVersion、ProjectId、project name、ページ順を保持する。
- `PageDocument`: PageId、Name、Canvas、object collectionを保持する。
- 選択、hover、zoom、open dialogなど一時UI状態は原則保存対象から分離する。
- `CanvasData`/`MojiData`は初期段階でadapter経由で包み、直ちに全面置換しない。

### PageEditorControl

- 現在の `PageDocument`を描画し、選択・hit test・drag transactionを扱うUserControlとする。
- `MainWindow`から `MainCanvas`、画像表示、文字panel管理、zoom、drop、page内command routingを移す。
- 親shellとはcommand/event interfaceで接続し、`MainWindow`への直接参照を持たない。
- 非active pageは必要に応じvisual treeをdetachし、文書状態だけ保持する。

## 3. 共通オブジェクトモデル

過剰な基底classは避け、serialize可能な共通componentを持つ。

```text
ObjectIdentity: Id, Type
TransformData: X, Y, Rotation (Sizeは型ごと)
ObjectState: ZIndex, IsLocked, IsVisible
RelationshipData: ParentId?, GroupId?
```

- `Id`はproject内で安定するUUIDを仮採用する。
- page所属はcontainerで表し、冗長なPageIdはcross-page参照が必要になるまで持たない案を優先する。
- `ZIndex`はpage内で一意な連番へ正規化し、list順と整合させる。
- 選択可能/hit test可能は型別policyであり、永続プロパティとしない。
- 文字は既存 `MojiData`を段階移行し、フキダシ・付加記号へ同じ描画class継承を強制しない。

## 4. Undo／Redo

Commandと小さいMementoの混合方式を推奨する。

- 追加/削除/並べ替え/リンク: semantic command。
- property変更: 変更前後valueのmemento。
- drag/resize/rotate: pointer downでbefore取得、move中はpreview、pointer upでafterを1件commit。
- text/数値連続入力: focus、idle interval、対象propertyを単位にcoalesceする。
- 自動layout: 入力条件と結果一式を1transactionにする。
- page内操作はPageHistory、page追加/削除/並べ替えはProjectHistoryへ置く案を仮採用する。active page切替時のUXをprototypeで検証する。
- 履歴entry数と推定byte数に上限を設け、保存時のrevision markerでdirtyを計算する。
- Undo履歴は保存しない。

## 5. 重なり順とグループ

- page object collection順をcanonical orderとし、serialize時にZIndexを正規化する。
- 「最前面/前面/背面/最背面」は同じcontainer内だけで作用する。
- フキダシ本体・しっぽ・文字linkは `BalloonGroup`の内部描画順を別途固定し、page-level ZIndexはgroupを1単位として扱う。
- 一般groupは`Ctrl+クリック`で選択したtop-level compositionを対象とするflatな明示groupとし、選択状態は保存せずgroup relationshipだけを保存する。nested group、group resize／rotation、range selectionは初期版で行わない。balloon link／mergeと同一概念へ統合しない。
- lock中は通常hit testとcommand対象から除外し、明示的な解除導線だけを残す。

## 6. 保存形式

新しいversioned manifest形式を導入し、旧形式はimporterで内部モデルへ変換する。

```text
manifest.xml
pages/
  {page-id}/
    page.xml
    image1.<ext>
    image2.<ext>   (optional)
```

- manifest: FormatVersion、MinimumReaderVersion、ProjectId、ページID/名前/順序。
- page.xml: Canvasとobject一覧。型discriminatorと安定IDを持つ。
- 旧ルート `CanvasData.xml`/`MojiData*.xml`はLegacy readerだけが読む。
- 新形式保存時は同一directoryの一時fileへ完全zipを作り、読戻し検証後にreplaceする。既存fileを先に削除しない。
- 未知major version、zip traversal、重複entry、異常sizeを検査する。
- 公式版互換exportは通常保存と分離し、対応可能性を調査してから提供する。

## 7. Unicodeと付加記号

- text segmentationは `System.Globalization.StringInfo`等を使うtext element単位へ変更する。
- Unicode結合文字は可能な限り1書記素として通常描画する。
- 漫画表現として個別調整が必要な記号は `AttachedSymbolData`として親文字anchorへ付ける。
- anchorを単なるUTF-16 indexにせず、text revisionとgrapheme indexの不一致を検出する。親本文変更時の再anchor policyを明示する。
- offsetはem基準、scale/rotation、装飾inherit flag、文字間隔へ含めるかを保存する。

## 8. フキダシ

- `BalloonData`: ShapeKind、Bounds、Fill、Stroke、StrokeThickness。
- `BalloonTailData`: TailId、tip、root parameter、width。TASK-160でordered collectionへ拡張し、旧single tailを0件または1件のcollectionへ移行する。merge完了後もmember別tail編集を許可する。bodyはmerge group全体だけをresize可能とし、member別resizeはunmerge後に許可する。
- `TextLinkData`: TextObjectId、layout mode、padding、minimum font size、alignment。
- 本体/しっぽ/linked textをpage上では1つのcompositionとして選択・Z変更する。
- Geometryは入力parameterをkeyにcacheし、drag中は簡易Geometry、終了後に確定Geometryを作る。
- 合体は元図形IDと合成parameterを保持する非破壊明示operationとし、TASK-150でballoon専用typed compositionとして実装する。一般groupとは分離する。

## 9. 自動レイアウト

feedback loopを避けるため、modeが責任方向を決める。

- `FitTextToFrame`: frame固定。wrap、center、必要時shrink。frameを変更しない。
- `FitFrameToText`: text size/手動改行固定。必要sizeを計測してframeだけを拡張する。
- manual adjustment後に自動処理を再実行するまで、勝手な再計算を行わないpolicyを検討する。
- 縦書きと横書きで同じinterfaceを使うが、測定strategyは分ける。
- 最小font size未満または収容不可はlayoutを破壊せず警告状態を返す。

## 10. クリップボードと画像

- clipboard accessはinterfaceで包み、STA/WPF固有処理をadapterへ隔離する。
- formatはPNG、DIB、Bitmap／BitmapSourceの順に、有効にdecodeできる最初の画像を採用する。
- image dataをmemory streamへcloneしてclipboard lifetimeから切り離す。
- 「新しいページ」「現在背景を置換」を明示し、置換は1 Undo transactionとする。
- alpha、DPI、pixel寸法を正規化する既存 `ImageUtil`との境界をテストする。

## 11. 性能・メモリ

- active pageだけを完全visualizeし、inactive page/projectはdocument中心に保持する。
- per-character BlurEffectの既存問題を計測し、機能追加と描画全面変更を同じtaskにしない。
- 文字背景ボックスの外側effectは第2枠線と全周blurだけを対象とし、canvas背景／背景画像／フキダシやhit領域を変更しない。
- Geometry cacheに上限を設け、font/style/DPI/text elementをkeyにする。
- historyはentry/byte上限、project closeでcache/history/window参照を解放する。

## 12. 移行順序

1. build/test baselineと保存characterization。
2. 原子的保存・安全な読込。
3. document/page modelとlegacy mapper。
4. PageEditorControl抽出。
5. project/page tab shell。
6. common ID/Z/lock。
7. Undo/dirty。
8. clipboard、付加記号、フキダシ、layout。
9. 一括出力、補助操作、将来機能。

詳細な依存は `task-breakdown.md`を原典とする。
