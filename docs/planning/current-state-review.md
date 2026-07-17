# 現状レビュー

調査基準日: 2026-07-17

基準コミット: `ca0bc2578fb9e76d9827cd073eec51ebd191df94`

## 1. 結論

要求機能はWPFの範囲で実現可能である。ただし現状は、単一の `MainWindow` がキャンバス、文字UI、編集状態、ファイルI/Oを直接統括し、保存対象データとWPFコントロールも密結合している。複数ページ、複数プロジェクトタブ、Undo、ZIndex、フキダシを同時に直接追加すると競合と回帰が集中するため、段階的に文書モデル、ページ編集境界、共通オブジェクト情報、履歴基盤を導入する必要がある。

## 2. Gitと環境

| 項目 | 結果 |
| --- | --- |
| OS | Microsoft Windows NT 10.0.26200.0 |
| Shell | Windows PowerShell 5.1.26100.8875 |
| Repository | `F:/github/MojiCollaTool_kattever` |
| 調査開始ブランチ | `main` |
| HEAD | `ca0bc2578fb9e76d9827cd073eec51ebd191df94` |
| 作業ツリー | clean（staged/unstaged/untrackedなし） |
| origin | `https://github.com/bncarrasco/MojiCollaTool_kattever.git` |
| upstream | `https://github.com/kuramiya/MojiCollaTool.git` |
| worktree | 調査開始時は主worktreeのみ |
| Git注意 | 所有者不一致のため、読み取りコマンドへ一時的な `safe.directory` を指定。global設定は未変更 |

## 3. ビルドと配布

- ソリューションは `MojiCollaTool/MojiCollaTool.sln`、SDK-style WPFプロジェクト1件である。
- `TargetFramework`は `net6.0-windows`、`UseWPF=true`、`Nullable=enable`、`OutputType=WinExe`。
- NuGet `PackageReference`、プロジェクト参照、`global.json`、lock fileはない。
- Publish profileはRelease/Any CPUのfolder publishを指定する。
- .NET 6.0.11/9.0.17ランタイムはあるがSDKは存在しない。
- `dotnet build ... -c Debug/Release`はSDK未検出で起動不能。
- Visual Studio Build ToolsのMSBuildでも `MSB4236: Microsoft.NET.Sdk が見つからない` ためDebug/Releaseとも失敗した。
- したがってコード由来の警告・エラー数、起動、手動操作は未検証である。
- `.gitignore`は `/OutputTest` のみで、通常の `bin/obj`を除外していない。成功ビルド前に運用方針を決める必要がある。

## 4. UI構造

- `MainWindow`にツールバー、単一 `MainCanvas`、画像2枚、文字一覧、キャンバス設定起動が集約される。
- 文字1件ごとに `MojiPanel`（WPF `ContentControl`）と `MojiWindow`（独立Window）が作られる。
- `CanvasEditWindow`は `CanvasData`を直接変更し、`MainWindow.UpdateCanvas()`を呼ぶ。
- データバインディングは限定的で、主な更新はイベントハンドラからモデルとコントロールを直接変更する方式である。
- ドラッグは `MouseDown`で捕捉し、`MouseMove`ごとに `MojiData.X/Y`を更新し、`MouseUp`で確定する。履歴境界はない。
- 選択モデル、共通編集コマンド、コンテキストメニュー、ZIndex操作、ロック、タブ、キーボードショートカットはない。
- `ObservableCollection<MojiPanel>`は表示用であり、文書データのコレクションではない。

## 5. データモデルと描画

### CanvasData / ImageData

- `CanvasData`はキャンバス寸法・色・余白・最大2枚の画像・画像2の配置を保持する。
- `ImageData`は元寸法と調整後寸法だけを保持し、画像実体は作業ディレクトリに置かれる。
- `CanvasData.Copy()`は `Image2LocatePosition`をコピーしておらず、将来Cloneを状態履歴に使用すると欠落する。

### MojiData / MojiPanel

- `MojiData`は整数ID、本文、位置、文字装飾、背景矩形、回転を保持する。
- 一意性は文字だけの連番IDに限定され、ページID、オブジェクト種別、ZIndex、ロック、親子・グループ、表示状態はない。
- `INotifyPropertyChanged`はなく、UI状態と保存状態の分離もない。
- `MojiPanel`が `MainWindow`と `MojiWindow`を直接参照し、モデル・ビュー・操作責務が混在する。

### 縦書きとUnicode

- 改行後の各行を `List<Char>`へ変換し、1 UTF-16 code unitごとに `DecoratedCharacterControl`を生成する。
- プールも `Dictionary<char,...>`であるため、サロゲートペア、結合文字、emoji sequence、書記素クラスターは正しく扱えない。
- 縦書きは特定文字集合に対する90度回転・移動のヒューリスティックで実装される。
- 文字は `FormattedText.BuildGeometry()`でGeometry化し、縁取りごとに `DrawingVisual`とBlurEffectを適用する。
- 既存メモにも、ぼかし利用時の描画負荷が主要な性能問題として記録されている。

## 6. 保存・読み込み

- 拡張子は `.mctzip`、実体はzipである。
- ルートへ `Info.txt`、`CanvasData.xml`、`MojiData{id}.xml`、`Image1.*`、任意の `Image2.*`を格納する。
- XMLは `XmlSerializer`、UTF-8 BOMなし。形式バージョン、ReaderVersion、ページmanifestはない。
- exe隣接の単一 `Working`ディレクトリを全プロジェクト状態として使用する。
- 保存時は既存出力ファイルを削除してからzipを作るため、圧縮失敗時に旧ファイルを失う。
- UIから文字を削除しても既存 `MojiData*.xml`を削除しないため、ロード後に文字を削除して保存すると古いXMLがzipへ残り、再読込時に復活し得る。
- 読み込みは現在のUIとWorkingを消してからzip展開・XML解析するため、破損ファイル読込失敗時に編集中状態を復元できない。
- zip entryの明示的なサイズ・パス・未知ファイル検証、原子的置換、バックアップはない。
- 文字ファイル列挙順がオブジェクト順となり、明示ZIndexがないため、保存前の重なり順は仕様化されていない。

## 7. Dirty、Undo、エラー処理

- dirty/saved revisionの概念はない。
- 終了確認は文字件数が1件以上かだけを見ており、保存済みでも毎回確認し、文字なしのキャンバス変更は確認しない。
- Undo/Redo履歴、変更コマンド、トランザクション、ドラッグ集約はない。
- 多くの上位操作は例外を捕捉して日本語ダイアログとexe隣接 `ErrorLog`へ出力する。
- 書込権限がないインストール先ではWorking、MojiFormat、ErrorLogが利用できない可能性がある。

## 8. テスト、CI、解析

- 自動テストプロジェクト、テストデータ、CI workflow、カバレッジ設定はない。
- Nullableは有効だが、警告をエラーにする設定、追加アナライザー、EditorConfigはない。
- 既存動作の基準シナリオは `docs/testing/verification-matrix.md`で未検証として定義する。

## 9. UI言語・製品表示・ライセンス

- 主要操作は日本語だが、`MojiWindow`、`Moji ID`、`Before/After`、ファイルダイアログfilter、`OK`など英語表示が残る。
- タイトル、README、配布案内に「MojiCollaTool 勝手版」および非公式フォークの注意書きがない。
- LICENSEは元作者のMIT copyrightを保持している。
- `ColorSelector`には `WpfColorPicker`由来と見られるコードがあり、既存メモはApache-2.0を指摘するが、ルートに第三者ライセンス告知が見当たらない。由来と必要な告知を確認する必要がある。

## 10. 主要ホットスポット

| 優先度 | 場所 | 問題 |
| --- | --- | --- |
| Critical | `DataIO.WriteWorkingDirToProjectDataFile` | 旧ファイル先行削除、非原子的保存 |
| High | `MainWindow.LoadProject` | 検証前に現在状態を破棄 |
| High | `DataIO.WriteMojiDatas` | 削除済み文字XMLが残留 |
| High | `MainWindow` / `MojiPanel` | 単一ページ前提とUI・状態の密結合 |
| High | `MojiPanel` / character pool | `char`単位でUnicode書記素非対応 |
| High | 保存形式全体 | バージョン、ページ、共通ID、ZIndexがない |
| Medium | dirty/close処理 | 保存状態を判定できない |
| Medium | Geometry/Blur | 入力・ドラッグ時の性能リスク |
| Medium | UI strings | 日本語と英語が混在 |

## 11. 実現可能性と段階的移行

全面書き換えは不要である。既存 `CanvasData`、`MojiData`、Geometry描画を一旦アダプターで包み、文書状態を先に導入する。旧形式は直接編集せず、一度内部の新モデルへ変換する。ページ編集コントロール抽出後に、プロジェクトタブ、ページタブ、共通オブジェクト、Undo、フキダシの順で進める。
