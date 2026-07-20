# ADR-0008: 一方向の自動レイアウトmode

- **Status:** Accepted
- **Context:** frameとtextが互いにサイズ変更するとlayout loopになる。またTASK-130時代のversion 2.2には自動layoutの明示適用stateがなく、既定`FitTextToBalloon`を新機能の適用済みと解釈できない。
- **選択肢:** 双方向自動追従、frame固定、text固定、明示mode。
- **Decision:** `FitTextToBalloon`と`FitBalloonToText`に加え、未適用を表す`Unapplied`を採用する。1回のlayoutで変更可能な側を一方に限定し、適用stateはversion 2.3で正式保存する。version 2.0〜2.2のlinkは読込時に全て`Unapplied`へ移行し、2.3〜2.4は3 stateを保持し、2.5以降は拒否する。
- **理由・利点:** 挙動が予測可能でUndoを1transactionにできる。
- **欠点・副作用:** mode選択UIと手動調整後policyが必要。
- **UI影響:** 「枠に合わせる」「文字に合わせる」等を日本語表示。
- **互換性影響:** mode、padding、minimum font size等を2.3新形式へ保存する。2.0〜2.2はtext content、geometry、style、relationship、Z、AttachedSymbol、Canvas、assetを変更せずlayout stateだけを移行する。旧archiveは読込だけでは書き換えない。
- **関連:** REQ-LAYOUT-001, REQ-BALLOON-MERGE-001, TASK-140, TASK-150。
- **移行:** 横書きmeasure→縦書きstrategy→balloon/background frame接続。2.2 fixtureのFitText／FitBalloon／missing／unknownをUnappliedへ移行し、2.3の3 state round-tripを検証する。
- **見直し条件:** usability testでmode理解が困難と判明した場合。
- **Supersedes / Superseded by:** なし。
