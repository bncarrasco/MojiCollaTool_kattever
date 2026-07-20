# 未決事項

重要な判断を推測で確定しないための台帳。`Temporarily decided`は実装開始条件で再確認する。

| ID | 質問 / 必要性 | 影響要件・task | 選択肢 | 推奨・仮決定 | 決定者 / 条件 | Status |
| --- | --- | --- | --- | --- | --- | --- |
| OQ-BUILD-001 | .NET 6を維持するかLTSへ更新するか | BUILD, TASK-000 | net6維持 / net8等へ移行 | 最初はnet6維持でbaseline、upgradeは別task | User/配布・SDK方針確認時 | Temporarily decided |
| OQ-NFR-001 | 起動・drag・page切替の数値閾値 | PERF, 000/120/140 | 絶対ms / baseline比 | 代表dataでbaseline比＋知覚閾値 | Commander/TASK-000結果後 | Open |
| OQ-NFR-002 | Working/log/cache保存先 | ERROR,SAVE | exe隣 / LocalAppData / project隣 | LocalAppDataを推奨、portable modeは別検討 | User/TASK-005前 | Open |
| OQ-FMT-001 | 新形式でも拡張子 `.mctzip`を継続するか | SAVE, 040 | 継続 / 新拡張子 | 誤読防止の新拡張子も比較 | User/format実装前 | Open |
| OQ-FMT-002 | 公式版互換exportを提供するか | COMPAT, 041 | 不提供 / 1page限定export | 初期はimportのみ、需要確認後に限定export | User/TASK-041後 | Deferred |
| OQ-FMT-003 | 保存前backupの既定 | SAVE,005/040 | 常時1世代 / optional / なし | 原子的replace＋1世代backup推奨 | User/TASK-005前 | Open |
| OQ-UNDO-001 | page履歴とproject履歴のUX | UNDO/PAGE,070 | 全project単一 / page別＋project別 / page別のみ | page別＋project operation historyをprototype | User/TASK-070設計時 | Temporarily decided |
| OQ-UNDO-002 | 履歴上限 | UNDO/MEM,070 | 件数 / byte / 両方 | 両方、初期値は計測で決定 | Commander/TASK-000後 | Open |
| OQ-UI-001 | ID/RGB/OK等の技術略語許容範囲 | UI,200 | 完全日本語 / 技術略語許容 | 技術略語・format名はすべて英語許容。一般英語UIは日本語化 | User | Decided |
| OQ-UI-002 | About表示形態 | FORK,190B | menu dialog / startup only / footer | menuの「このアプリについて」 | User/TASK-190B前 | Temporarily decided |
| OQ-UI-003 | project/page tabの表示 | WORKSPACE,030 | 二段 / 単一bar group / 別Window | 二段tab。単一bar groupは将来 | User/usability確認時 | Temporarily decided |
| OQ-UI-004 | page単位dirty印を表示するか | DIRTY,030 | projectのみ / 両方 | projectとpageの両方に`*`を表示 | TASK-030固定UX。保存成功時にproject配下をclear | Decided |
| OQ-UI-005 | locked objectの選択解除導線 | LOCK,080 | list / modifier click / context cycling | 小型object listまたはmodifierを比較 | User/TASK-080前 | Open |
| OQ-BAL-001 | 初期フキダシshape | BALLOON,110/120 | 4種 / 思考含む5種 | 楕円・角丸四角・四角・モノローグの4種 | User/TASK-110前 | Temporarily decided |
| OQ-BAL-002 | balloon text linkと一般groupの関係 | BALLOON/GROUP,130/230 | 同一 / 別概念 | 別概念、compositionとして扱う | Commander/TASK-130設計時 | Temporarily decided |
| OQ-LAYOUT-001 | 改行・禁則の初期範囲 | LAYOUT,140 | 単純measure / 基本禁則 / 高度組版 | まず明示改行＋基本wrap、禁則は計測後 | User/TASK-140前 | Open |
| OQ-CLIP-001 | clipboard format優先順位 | CLIPBOARD,170 | PNG / DIB / BitmapSource | PNG→DIB→Bitmap／BitmapSourceの順 | User | Decided |
| OQ-GROUP-001 | 初期multi-select／一般groupの操作範囲 | SELECT/GROUP,230 | flat / nested、click / range、moveのみ / transform | Ctrl+click、flat group、group保存、move／Z／lock／delete／duplicate。range、resize、rotationは対象外 | User | Decided |
| OQ-TAIL-001 | 合体中の複数tail編集 | BALLOON-TAILS,160 | unmerge必須 / merge維持で個別編集 | merge完了後は各memberの各tailを個別編集可能。body個別resizeは禁止継続 | User | Decided |
| OQ-BGFX-001 | 外側effectの対象とhit範囲 | BACKGROUND-FX,180 | 文字背景 / canvas / image、hit拡張有無 | 文字背景ボックスだけ。第2枠線／全周blur、hit範囲は拡張しない | User | Decided |
| OQ-EXPORT-001 | 一括出力file名 | EXPORT,050 | index-page / page名 / prompt | zero-padded index＋sanitized page名 | User/TASK-050前 | Temporarily decided |
| OQ-LICENSE-001 | WpfColorPicker由来と必要notice | FORK/DEPS,190A | Apache source一致 / 独自 / 不明 | 一次sourceとdiffで確認、断定しない | Worker/TASK-190A | Open |
