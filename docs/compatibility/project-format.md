# プロジェクト形式と互換性仕様

Status: Current versioned format 2.4 / legacy compatibility maintained

## 1. 現行公式系形式（コード観測）

- **拡張子:** `.mctzip`
- **container:** zip。directory自体ではなくWorking directoryの内容をrootへ格納する。
- **serializer:** .NET `XmlSerializer`、UTF-8 BOMなし。
- **作業場所:** 実行file隣の `Working`。

### Root entries

| Entry | 必須性 | 内容 |
| --- | --- | --- |
| `Info.txt` | writerは作成、readerは参照しない | `SoftwareName`, assembly version, timestamp |
| `CanvasData.xml` | 必須 | CanvasData XML |
| `MojiData{id}.xml` | 0件以上 | 文字objectごとのXML |
| `Image1.<source-ext>` | 通常1件 | 背景画像1 |
| `Image2.<source-ext>` | 任意 | 連結背景画像2 |

readerはInfoのversionを互換判定に使用せず、形式versionやMinimumReaderVersionは存在しない。

### CanvasData fields

`CanvasWidth`, `CanvasHeight`, `ImageData1`, `ImageData2`, `Image2LocatePosition`, `ImageMarginTop/Left/Bottom/Right`, `CanvasColor`。computed propertyがXmlSerializerへどのように出力されるかはfixtureで固定する。

### ImageData fields

`OriginalWidth`, `OriginalHeight`, `ModifiedWidth`, `ModifiedHeight`。画像file名やhashはXMLへ保持しない。

### MojiData fields

`Id`, `FullText`, `X`, `Y`, `FontSize`, `FontFamilyName`, `TextDirection`, `IsBold`, `IsItalic`, `CharacterMargin`, `LineMargin`, `ForeColor`, 1/2番目の縁取り色・幅・blur、背景box表示・色・padding・枠・corner radius、`RotateAngle`。computed propertyのXML出力はfixture確認対象。

### 現行の互換性risk

- zipを作る前に出力先を削除し、原子的保存・backupがない。
- Workingのstale `MojiData*.xml`が再zipされ得る。
- load前検証がなく、現在sessionを破棄後に失敗し得る。
- object order/ZIndexが保存されない。Directory enumeration順は仕様としない。
- version gateがなく、未知field/versionのpolicyがない。
- zip traversal、重複entry、異常展開sizeの明示検査がない。
- 日本語path、Unicode filename、欠落entry、破損XMLは自動test未実施。

## 2. 勝手版新形式（現行）

拡張子はOQ-FMT-001で未決。containerはzipを継続する。

```text
manifest.xml
pages/
  5e99.../
    page.xml
    image1.png
    image2.jpg       # optional
```

### Manifest

最低限、以下を英語内部keyで保持する。

- `FormatVersion`: semanticなmajor/minor。writerの現行値は `2.4`。readerは `2.0`〜`2.4`を受け付け、`2.5`以降と未知majorを拒否する。
- `MinimumReaderVersion`: readerが安全に開くためのminimum。`2.5`以降は拒否する。
- `ProjectId`: UUID。
- `Product`: `MojiCollaTool Katteban`等の内部識別子。表示名とは分離。
- `Pages`: `PageId`, `Name`, `Order`, `RelativePath`。

### Page

- `PageId`, `Name`。
- `Canvas`と画像metadata。
- 順序付き `Objects`。
- 各objectに `Id`, `Type`, transform, ZIndex, IsLocked。
- 型別data: text、attached symbol、balloon、tail/link等。
- page-level relationshipとして、2.4以降は`BalloonMerges`にstable merge ID、primary balloon ID、順序付きmember balloon IDを保存する。これはpage objectや一般`GroupId`ではない。
- 選択、hover、zoom、open Windowは保存しない。page dirty/historyも保存しない。

### Layout state compatibility

`TextLinkData.LayoutMode`はversion 2.3で`Unapplied`、`FitTextToBalloon`、`FitBalloonToText`の3 stateを正式保存する。version 2.0〜2.2のlinkは保存値がFitText、FitBalloon、missing、unknownのいずれであってもreaderが`Unapplied`へ移行する。これはTASK-130以前に自動layoutが存在せず、2.2 fieldだけでは旧既定値と明示Applyを判別できないためである。

このmigrationはlayout stateだけを変更し、text content、position、font/style、balloon geometry/style/rotation/tail、padding、minimum font、alignment、relationship、Z、AttachedSymbol、Canvas、assetを変更しない。旧archiveは読込だけでは書き換えず、次回の明示保存時に2.4として出力する。

### Balloon merge compatibility

version 2.4では、各merge groupを`MergeId`、`PrimaryBalloonId`、2件以上のordered `MemberIds`として保存する。memberのshape、position、size、rotation、style、tail、text link、layout stateは従来どおり各`BalloonData`に保持し、merge／unmergeで書き換えない。

version 2.0〜2.3はmerge collectionなしとして読む。2.4 archiveにempty ID、2件未満、primary不在、missing／duplicate member、同一balloonの複数group所属、project内ID衝突があれば全modelのcommit前に拒否し、silent repairしない。旧archiveと既存working dataは読込失敗で変更しない。

## 3. Reader policy

1. 原fileを変更せず、pathとextensionだけで形式を断定しない。
2. zip central directoryを読み、絶対path、`..`、重複entry、上限超過を拒否する。
3. manifestがあればFormatVersion/MinimumReaderVersionを検査する。
4. manifestがなく、現行root entriesを満たす場合だけlegacy readerへ渡す。
5. 一時modelへ全entryを読込・validateする。
6. 成功後にだけ新 `ProjectSession`としてworkspaceへ追加する。
7. 失敗時は既存sessionとWorking/cacheを変更せず、日本語errorと診断logを残す。

未知future **major** versionと2.5以降のfuture **minor** versionはread-only推測せず拒否する。未知optional **minor** fieldはserializer policyとfixtureで安全性を確認してから許容する。

## 4. Writer policy

1. 保存先と同一volume/directoryにrandomな一時fileを作る。
2. 現在documentだけから全entryを書き、共有Working directoryをsourceにしない。
3. zipをcloseし、manifestとentry一覧を読戻しvalidateする。
4. optional backup policyを適用する。
5. OSで利用可能な原子的replaceを使用し、unsupported時も旧fileを先に消さないfallbackを使う。
6. directory/file streamをflushし、失敗時は一時fileを隔離/削除して旧fileを保持する。

file名はUUID directoryを使用し、page名をarchive pathへ直接使用しない。日本語page名はXML値としてUTF-8で保持する。

writerはmanifestの`FormatVersion`と`MinimumReaderVersion`へ`2.4`を出力し、2.3で定義したlayout stateと2.4のballoon merge relationshipをそのまま保存する。

## 5. 互換性matrix

| File | 公式/現行版 | 勝手版 |
| --- | --- | --- |
| 旧単page mctzip | 読込可（基準、未実行） | 1pageへimport必須 |
| 勝手版new format 2.0〜2.2 | 読込不可と仮定 | 読込可。layout stateはUnappliedへ安全移行 |
| 勝手版new format 2.3 | 読込不可と仮定 | 読込可。3 stateをround-trip |
| 勝手版new format 2.4 | 読込不可と仮定 | 読込可。layout 3 stateとballoon mergeをround-trip |
| 勝手版new format 2.5以降 | 読込不可と仮定 | 安全に拒否 |
| 勝手版から旧形式export | 未提供 | OQ-FMT-002でDeferred |
| 未知future major | 不明 | 安全に拒否 |

「公式版で開ける」と表示するのは実fixtureで往復確認した場合に限る。

## 6. 移行とdata保護

- 旧形式を開いただけでは原fileを書換えない。
- version 2.0〜2.3を開いただけではarchiveを書換えず、次回の明示保存で2.4へ出力する。2.0〜2.2のlayout stateだけは読込model上でUnappliedへ安全移行する。
- 初回保存時は新形式の別file名を既定とし、旧file上書きは明示確認を検討する。
- 変換warningと非互換点を日本語で表示する。
- 保存前backup、temp naming、cleanup、disk full/permission/locked fileをfault testする。
- 日本語path、長いpath、Unicode page/file metadataをround-trip testする。
