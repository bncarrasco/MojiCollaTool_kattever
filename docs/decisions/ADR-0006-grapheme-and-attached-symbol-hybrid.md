# ADR-0006: 書記素処理と独自付加記号の併用

- **Status:** Proposed
- **Context:** 現状はchar単位。Unicode結合文字だけでは漫画用途の任意offset・inherit解除・縦書き調整を表現できない。
- **選択肢:** Unicode結合文字のみ、独自子objectのみ、併用。
- **Decision:** text segmentationをgrapheme単位にし、通常結合文字はgraphemeとして描画する。個別調整が必要な表現はAttachedSymbolDataとして親anchorへ付ける。
- **理由・利点:** Unicodeの正しさと漫画表現の手動調整を両立する。
- **欠点・副作用:** 親text編集時のanchor再対応、font差、hit testが複雑。
- **UI影響:** 付加記号、装飾を継承、位置、大きさ、回転を日本語表示。
- **互換性影響:** 新形式へ子dataを保存。旧形式には存在しない。
- **関連:** REQ-SYMBOL-001, REQ-NFR-UNICODE-001, TASK-090, TASK-100。
- **移行:** grapheme characterization→model→描画/UI。
- **見直し条件:** WPF FormattedTextのgrapheme描画結果が要求を満たさない場合。
- **Supersedes / Superseded by:** なし。
