# ADR-0007: フキダシを非破壊compositionとして扱う

- **Status:** Proposed
- **Context:** 本体、しっぽ、linked textは一体操作が必要だが、将来の複数tailと合体解除で元情報を失ってはいけない。
- **選択肢:** (A) 1つの焼き込みGeometry、(B) 本体/tail/link dataのcomposition、(C) 一般groupだけで表現。
- **Decision:** B。型別dataを保持し、page-levelでは1 compositionとして選択・Z変更する。合体は元dataを保持する明示operation。
- **理由・利点:** 編集、Undo、cache、非破壊解除、将来複数tailに対応しやすい。
- **欠点・副作用:** 内部hit testと描画順が複雑。
- **UI影響:** フキダシ追加、しっぽ追加/削除、合体/解除を日本語表示。
- **互換性影響:** 新形式へshape parameterとrelationshipを保存。
- **関連:** REQ-BALLOON-001/002, REQ-BALLOON-MERGE-001, TASK-110〜160。
- **移行:** 本体model→描画→single tail→text link→将来合体。
- **見直し条件:** WPF Geometry性能測定でparameter再生成が許容不可の場合。
- **Supersedes / Superseded by:** なし。
