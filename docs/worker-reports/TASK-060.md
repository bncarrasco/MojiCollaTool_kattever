# TASK-060 実装報告

## Result summary

`MojiData`へ共通オブジェクトID（UUID）、種別、ZIndex、ロック、表示、親/グループ参照を追加し、`PageDocument`をcanonical object orderの境界にしました。旧int IDは互換値として保持し、新形式のUUIDを併記します。

## Git kickoff

- Task: TASK-060
- Branch: `feature/TASK-060-object-id-zindex`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-060`
- Functional base / kickoff HEAD: `1e64a715adeb2102f15b6a932075f5afdb65ff44`
- Result commit: `96b89e6` (`feat: add common object identity and z-order`)
- Dedicated `TASK-060.md`: 未配置。task-breakdown、requirements、ADR-0003を根拠に実装。

## 対応要件

- REQ-OBJECT-001: UUID、Type、state、relationship fieldsを永続化対象へ追加。
- REQ-ZORDER-001: Page内list順を正とし、ZIndexを0始まりの連番へ正規化。
- ADR-0003: 深い継承階層を作らず、既存MojiDataへ共通状態を段階導入。

## 変更ファイル

- `MojiData.cs`: `ObjectId`, `Type`, `ZIndex`, `IsLocked`, `IsVisible`, `ParentId`, `GroupId`を追加。deep copyと新規複製時のID扱いを分離。
- `PageDocument.cs`: object ID重複検出、legacy空ID修復、ZIndex正規化、ID検索、複製時のrelationship remap、保存用snapshotを追加。
- `ProjectDocument.cs`: project内のobject ID重複を拒否。
- `VersionedProjectFormat.cs`: page.xmlへcanonical object snapshotを保存。
- `ObjectIdentityAndZOrderTests.cs`: default、normalize、duplicate、clone、versioned round-tripを追加。

## 検証

- `git diff --check`: pass。
- `dotnet test tests/MojiCollaTool.Tests/MojiCollaTool.Tests.csproj --no-restore`: 未実行。環境に.NET SDK 6.0.428がなく、`global.json`のSDK解決で停止。
- Debug/Release build: 未実行。上記SDK不足のためNot verified。
- UI/manual verification: Scope外（UI変更なし）。

## 既知の制約

- UIのZ操作・lock操作・Undo連携は後続TASK-070/080の対象。
- 旧形式読込時のObjectIdは旧XMLに値がなければ新規生成される。既存int IDは保持する。

## Rollback

本taskの実装コミットをrevertし、`MojiData`旧形式と既存serializerへ戻す。
