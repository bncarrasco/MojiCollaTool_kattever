# ADR-0003: 共通オブジェクトIDとZIndex

- **Status:** Proposed
- **Context:** 現行int IDは文字だけで、Canvas children追加順が暗黙Z順となる。link、lock、Undo、保存には安定参照が必要。
- **選択肢:** (A) 型別連番、(B) 共通UUIDと共通component、(C) 大規模継承hierarchy。
- **Decision:** B。UUID、Type、Transform、ZIndex、Lock、Parent/Group参照を共通componentとして段階導入する。
- **理由・利点:** 型を跨ぐ安定参照と決定的な保存順を得つつ、過剰な継承を避ける。
- **欠点・副作用:** 旧int IDとのmappingとZIndex正規化が必要。
- **UI影響:** Z/lock操作を日本語で追加。
- **互換性影響:** 新形式へID/Zを保存。旧読込時に生成。
- **関連:** REQ-OBJECT-001, REQ-ZORDER-001, REQ-LOCK-001, TASK-060, TASK-080。
- **移行:** 文字adapterから導入し、新型へ共有。
- **見直し条件:** XML serializerとの相性または可読性に重大問題がある場合。
- **Supersedes / Superseded by:** なし。
