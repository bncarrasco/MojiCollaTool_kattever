# ADR-0005: バージョン化新形式と旧形式import

- **Status:** Accepted
- **Context:** 現行zipは単page root構造でversionがなく、上書き時に旧fileを先に削除する。勝手版のversioned形式は2.3で自動レイアウトの明示適用stateを正式保存する。
- **選択肢:** (A) root構造へ無制限追加、(B) versioned manifestとpage directory、旧形式import、(C) 単一巨大XML。
- **Decision:** B。manifestとpages directoryを持つ新形式を通常保存し、旧形式は専用readerから内部modelへ変換する。versioned writerは2.3を出力し、2.0〜2.2は読込時にlayout stateだけを`Unapplied`へ移行する。2.4以降はreaderで拒否する。
- **理由・利点:** version判定、複数page、安定ID、未知version拒否、段階移行が明確。TASK-130以前の2.2既定`FitTextToBalloon`を明示Apply済みと誤認せず、旧projectを開いただけのwrap変化を防げる。
- **欠点・副作用:** 2.0〜2.2で保存されていたlayout modeの意図は復元できず、次回保存時に2.3としてlayout stateが`Unapplied`になる。公式版は新形式を読めず、compat exportを別途検討する必要。
- **UI影響:** 旧形式移行・未知version・保存失敗を日本語表示。
- **互換性影響:** 旧形式readはMust。versioned 2.0〜2.2は読込可能でlayout stateのみ安全移行する。2.3は3 stateをround-tripし、2.4以降とfuture minimum readerは拒否する。新形式→公式版は非互換を明示。
- **関連:** REQ-COMPAT-001, REQ-SAVE-001, TASK-005, TASK-040, TASK-041。
- **移行:** golden fixture固定→safe writer→manifest reader/writer→legacy mapper。旧archiveはread-onlyで変更せず、明示保存時だけ2.3へ出力する。
- **見直し条件:** 公式版の将来形式または実fixtureが想定と異なる場合。
- **Supersedes / Superseded by:** なし。
