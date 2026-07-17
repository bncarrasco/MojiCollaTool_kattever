# ADR-0008: 一方向の自動レイアウトmode

- **Status:** Proposed
- **Context:** frameとtextが互いにサイズ変更するとlayout loopになる。
- **選択肢:** 双方向自動追従、frame固定、text固定、明示mode。
- **Decision:** `FitTextToFrame`と`FitFrameToText`の明示modeを採用し、1回のlayoutで変更可能な側を一方に限定する。
- **理由・利点:** 挙動が予測可能でUndoを1transactionにできる。
- **欠点・副作用:** mode選択UIと手動調整後policyが必要。
- **UI影響:** 「枠に合わせる」「文字に合わせる」等を日本語表示。
- **互換性影響:** mode、padding、minimum font size等を新形式へ保存。
- **関連:** REQ-LAYOUT-001, TASK-140。
- **移行:** 横書きmeasure→縦書きstrategy→balloon/background frame接続。
- **見直し条件:** usability testでmode理解が困難と判明した場合。
- **Supersedes / Superseded by:** なし。
