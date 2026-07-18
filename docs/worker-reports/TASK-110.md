# TASK-110 フキダシ基本モデル 実装報告

## Result

実装完了。Implementation commit: `90b0b63e2cb0a0b91253cf4bf199b73938f1c732`。

## Summary

- `BalloonData`、`BalloonTailData`、`TextLinkData`とshape/layout/alignment discriminatorを追加。
- 文字とフキダシの共通ID、位置、回転、ZIndex、lock、visibility、parent/group stateを保持し、PageDocumentでページ全体のcanonical Z順を管理。
- Page cloneでballoon/tail/text linkのdeep copyとrelationship ID remapを実装。文字削除時はフキダシを残してtext linkだけ解除。
- versioned `page.xml`へBalloons collectionを追加。旧page.xmlは空collectionとして読み、未知shapeはUnknownとして保持し、無効なrelationshipは安全に解除。
- `BalloonCommands`をProjectSessionのUndo/Redoへ接続し、add/remove/update/tail/link/unlinkを1 operationとして記録。
- 描画、Geometry、hit test、handle、MainWindow/PageEditor UIはTASK-120/130の範囲として変更していない。

## 対応要件

`REQ-BALLOON-001`、`REQ-BALLOON-002`、`REQ-OBJECT-001`、`REQ-ZORDER-001`、`REQ-UNDO-001`。

## 開始時Git確認

- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-110`
- Branch: `feature/TASK-110-balloon-model`
- Functional base / 開始HEAD: `aa16cbc774b578ccd53b5101d919a44f546c63d8`
- 開始時status: clean
- SDK: `6.0.428`

## 変更ファイル

- `MojiCollaTool/MojiCollaTool/Document/BalloonData.cs`
- `MojiCollaTool/MojiCollaTool/Document/PageDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/ProjectDocument.cs`
- `MojiCollaTool/MojiCollaTool/Document/VersionedProjectFormat.cs`
- `MojiCollaTool/MojiCollaTool/MojiData.cs`
- `MojiCollaTool/MojiCollaTool/Workspace/BalloonCommands.cs`
- `tests/MojiCollaTool.Tests/BalloonModelTests.cs`
- `tests/MojiCollaTool.Tests/BalloonPersistenceTests.cs`
- `docs/testing/verification-matrix.md`
- `docs/planning/implementation-ledger.md`
- `CHANGELOG.md`

## 設計判断

- 公開モデルはsingle-tail slotとし、将来の複数tail拡張を妨げないDTO境界にした。
- 文字とフキダシは既存のMojiData APIを壊さず別collectionで保持し、`AllObjects`でcanonical page-level orderを公開した。
- unknown shapeはarchive全体を拒否せず`Unknown`と原文字列を保持する。unknown relationshipは対象を勝手に生成せずdetachする。
- numeric validationはNaN/Infinity、負のbounds、stroke/tail width、root parameter範囲を拒否する。update APIはcandidateを検証してから置換する。
- legacy XMLにBalloonsがない場合は空collectionとし、既存形式の読込を維持する。

## ADR候補

なし。OQ-BAL-001の4 shape、OQ-BAL-002のballoon compositionと一般groupの分離、single-tail方針を実装した。

## ビルド・テスト

- `C:\Users\user\.dotnet\dotnet.exe --version`: `6.0.428`
- Debug build: pass、0 warnings、0 errors
- Debug test: pass、110/110
- Release build: pass、0 warnings、0 errors
- Release test: repositoryの`eng/test.ps1`はDebug configurationを固定するため、Release build後にも同scriptでDebug testを実行する
- `git diff --check`: pass

## 手動・互換性確認

- 横書き／縦書きUnicode textとballoon linkのmodel/round-trip: automated verified
- 日本語project/page名、日本語pathのversioned round-trip: automated verified
- 旧versioned page.xml、legacy root-entry形式、画像asset: existing regression tests pass
- GUI描画、Geometry、hit test、handle、実Window操作: Not verified（TASK-110 scope外）
- 日本語UI: Not applicable（新規UIなし）

## 既知の問題・後続影響

- unknown shapeは保存時もUnknown文字列を保持するが、描画fallbackはTASK-120で決定する。
- tailの編集handleとlinkされた文字の一体移動はTASK-130で実装する。
- 自動layoutはTASK-140で実装する。

## マージ注意・ロールバック

- `PageDocument`、`ProjectDocument`、`VersionedProjectFormat`に変更があるため、後続TASK-120/130の同ファイル変更は本実装を取り込んでから行う。
- rollbackはResult commitをrevertし、既存のT060/T070モデル・履歴基盤は維持する。
