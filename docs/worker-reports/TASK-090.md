# TASK-090 書記素・付加記号データモデル 実装報告

## Result

- Base commit: `384dcfc76ef39871daa47c91ef6bd5085afafc33`
- Result implementation commit: to be filled after the implementation commit
- Report finalization commit: to be filled after the report update

- Grapheme segmentation serviceを追加し、サロゲート、結合文字、variation selector、emoji modifier、ZWJ sequence、regional indicator、CRLFを1書記素単位として扱うようにした。
- `AttachedSymbolData`を追加し、親text object ID、grapheme anchor、anchor fingerprint、em offset、scale、rotation、inherit flags、character spacing policyを保持するようにした。
- `PageDocument.SetMojiDatas()`を含む本文変更経路へanchor reconciliationを接続し、text変更時はfingerprintを使ってanchorをreanchor／detach／remove／rejectできるようにした。
- ReanchorToNearest以外のDetach／Remove／Rejectでは検索を行わず、同一書記素の複数候補は任意選択せずdetachまたはrejectするようにした。
- 文字・フキダシ・付加記号を横断するObjectId検証を候補状態へ適用してからcommitするようにし、重複ID・親ID・不正数値入力時の非破壊性を確保した。
- `ProjectSession`の既存履歴境界に`AttachedSymbolCommands`を接続し、add/update/remove/reanchorをUndo/Redo可能にした。
- versioned project formatを2.2へ更新し、`AttachedSymbols`をpage XMLへ保存するようにした。2.0／2.1は従来どおり読込可能で、2.3以降は拒否する。
- `AttachedSymbolData`の明示色はWPF `Color`ではなく`uint ForeColorArgb`とし、モデルをWPF非依存にした。

## Validation

- Debug test: 136 passed, 0 failed。
- Release test: 136 passed, 0 failed。
- `git diff --check`: pass。
- UI/rendering、付加記号のドラッグ／選択／表示はTASK-100の範囲として未検証。

## Files

- `Document/GraphemeService.cs`
- `Document/AttachedSymbolData.cs`
- `Document/PageDocument.cs`
- `Document/ProjectDocument.cs`
- `Document/VersionedProjectFormat.cs`
- `Workspace/AttachedSymbolCommands.cs`
- `tests/MojiCollaTool.Tests/TASK090AttachedSymbolTests.cs`

## Known follow-up

- `PageEditorControl`やrendererへの実表示接続はTASK-100で実施する。
- フォント依存の縦書き配置golden testはUI laneで追加する。
