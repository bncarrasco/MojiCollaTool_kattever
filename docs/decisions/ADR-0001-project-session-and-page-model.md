# ADR-0001: ProjectSessionと複数ページ文書モデル

- **Status:** Accepted
- **Context:** 現状はMainWindowが単一CanvasDataとMojiPanel一覧を所有する。要求は複数projectを同一アプリで開き、各projectが複数pageを持つ。
- **選択肢:** (A) MainWindow状態をtabごとに複製、(B) ProjectSession/ProjectDocument/PageDocumentを導入、(C) projectごとに別process。
- **Decision:** B。ApplicationWorkspaceが複数ProjectSessionを持ち、ProjectDocumentが順序付きPageDocumentを持つ。UIはproject tabとpage tabの二段構成。
- **理由・利点:** 保存・dirty・history・file pathを隔離でき、複数Windowより軽量。文書階層が明確。
- **欠点・副作用:** 初期抽出量が増え、既存MainWindow直結codeにadapterが必要。
- **UI影響:** 上段project tab、下段page tab。新規文言は日本語。
- **互換性影響:** 旧単pageはpage 1へ変換。新形式が必要。
- **関連:** REQ-PAGE-001, REQ-WORKSPACE-001, TASK-010, TASK-025, TASK-030, TASK-040。
- **移行:** まず内部model、次にPageEditorControl、最後にtab shell。
- **見直し条件:** 二段tabのusable testで重大な混乱が確認された場合。
- **Supersedes / Superseded by:** なし。
