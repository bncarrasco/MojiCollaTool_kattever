# ADR-0002: ページ編集コントロールの境界

- **Status:** Proposed
- **Context:** MainWindowがCanvas、画像、文字、zoom、drop、dialogを直接扱い、tabごとの独立状態を作れない。
- **選択肢:** (A) MainWindowを複製、(B) PageEditorControlへpage内責務を抽出、(C) 全面MVVM化。
- **Decision:** B。page描画・選択・page内inputをUserControlへ抽出し、shellとはcommand/eventで接続する。全面MVVM化はしない。
- **理由・利点:** 既存描画を段階的に移せ、tab再利用とtest seamを得る。
- **欠点・副作用:** 移行中はadapterと一時的な二重経路が生じる。
- **UI影響:** 原則なし。tab内へ既存editorを収める。
- **互換性影響:** なし。
- **関連:** REQ-PAGE-001, REQ-WORKSPACE-001, TASK-020, TASK-030。
- **移行:** Canvas/zoom→object host→dialog接続の順。
- **見直し条件:** 抽出時にWPF Window依存が分離不能と判明した場合。
- **Supersedes / Superseded by:** なし。
