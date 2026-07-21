# 要件定義

Status: Initial baseline

分類: Must / Should / Could / Won't for now

## 1. 前提

- 製品は軽量な文字コラ・漫画組版ツールであり、汎用画像編集ソフトを目指さない。
- 新規ユーザー向けUIは日本語、識別子と保存内部キーは英語とする。
- 旧 `.mctzip`を失わず読み込めることを優先し、新形式を旧公式版が読めるとは仮定しない。
- 「複数プロジェクト」は、上段のプロジェクトタブと、各プロジェクト内のページタブによる二段構成を仮案とする。
- α版のレビュー／Done判定は`alpha-release-policy.md`に従う。「より良くできる」という改善余地だけでDoneやlocal package生成を保留しない。

## 2. Must

### REQ-UI-JA-001 日本語UI

- **価値・詳細:** 新規のタイトル、タブ、メニュー、ボタン、ダイアログ、エラー、警告、ツールチップ、状態表示を日本語にする。既存英語UIは統一タスクで修正する。
- **依存:** なし。
- **受け入れ条件:** 新規UIレビューで意図しない英語文言が0件。入力文字列は改変しない。
- **非対象・影響:** 識別子と内部キーの日本語化はしない。UI影響あり、形式影響なし。
- **決定・検証:** ID、RGB、PNG、JPEG、DPI、IME、UI、URL、UUID、XML、ZIP、OK等の技術略語・format名は英語表記を許可する。一般英単語、説明文、操作名は日本語化する。XAML/C#文字列走査と手動確認を行う。

### REQ-FORK-001 非公式フォーク表記

- **価値・詳細:** README、タイトル、About、配布物に「MojiCollaTool 勝手版」、非公式、原作者と無関係、原作者へ問い合わせない旨を表示し、元著作権とMITを維持する。
- **依存:** REQ-UI-JA-001。
- **受け入れ条件:** 4箇所の表記と第三者ライセンス確認が完了する。
- **非対象・影響:** 公式を装う名称は使用しない。UI/配布影響あり、形式影響なし。
- **未決・仮定・検証:** About UI形態はOQ-UI-002。配布物レビュー。

### REQ-COMPAT-001 旧単ページ形式の読込

- **価値・詳細:** 現行 `.mctzip`を1ページの新内部モデルとして読み込み、画像・CanvasData・MojiDataを保持する。
- **依存:** REQ-SAVE-001、REQ-PAGE-001。
- **受け入れ条件:** golden fixtureを読込・再描画でき、原本を変更しない。未知/破損形式で現在状態を失わない。
- **非対象・影響:** 新形式を公式版で開ける保証はしない。保存形式影響大、UIは移行通知のみ。
- **未決・仮定・検証:** 公式版互換書出しはOQ-FMT-002。fixture自動テストと手動比較。

### REQ-SAVE-001 安全でバージョン化された保存

- **価値・詳細:** Version/MinimumReaderVersionを持つmanifestを採用し、一時ファイル作成、検証、原子的置換または安全なfallback、任意バックアップを行う。
- **依存:** REQ-COMPAT-001。
- **受け入れ条件:** 保存途中の例外で既存ファイルが残り、削除済みオブジェクトが復活しない。未知future versionを拒否できる。
- **非対象・影響:** クラウド同期・同時共同編集は対象外。保存形式影響大、失敗UIは日本語。
- **未決・仮定・検証:** バックアップ既定値はOQ-FMT-003。fault injection、自動テスト、日本語パス確認。

### REQ-PAGE-001 1プロジェクト複数ページ

- **価値・詳細:** ページID、名前、順序、背景、キャンバス設定、オブジェクト、選択、dirty、必要な履歴をページ単位で保持する。
- **依存:** REQ-SAVE-001、REQ-OBJECT-001。
- **受け入れ条件:** 追加・複製・削除・名前変更・並べ替え・切替・再読込後の順序保持ができる。初期名は `01`, `02`形式。
- **非対象・影響:** ページ間連続キャンバスは対象外。UI/形式影響大。
- **未決・仮定・検証:** ページ削除履歴はREQ-UNDO-001とOQ-UNDO-001。自動テストと手動タブ操作。

### REQ-WORKSPACE-001 複数プロジェクトのタブ管理

- **価値・詳細:** 同一アプリ内で複数 `ProjectSession`を上段タブとして開き、その中でページタブを管理する。
- **依存:** REQ-PAGE-001、REQ-DIRTY-001。
- **受け入れ条件:** プロジェクトごとにFilePath、dirty、履歴、選択を隔離し、個別に開く・保存・閉じる。同一ファイルの二重オープンを防ぐ。
- **非対象・影響:** 初期版ではChrome型の単一バー折りたたみグループ、複数Window、プロジェクト間ページ移動は対象外。UI影響大、形式影響なし。
- **未決・仮定・検証:** Must扱いはユーザー追加要求により採用。二段タブを仮決定しOQ-UI-003で見直す。複数session手動・自動状態テスト。

### REQ-DIRTY-001 保存状態管理

- **価値・詳細:** saved revisionと現在revisionを比較し、ページとプロジェクトの未保存状態を表示する。
- **依存:** REQ-UNDO-001、REQ-PAGE-001。
- **受け入れ条件:** 保存後に印が消え、Undo/Redoでsaved revisionへ戻ると正しく反映。閉じる時は対象プロジェクトだけ確認。
- **非対象・影響:** 自動保存は初期対象外。UI影響あり、形式影響なし。
- **未決・仮定・検証:** ページ個別印の表示方法はOQ-UI-004。状態遷移テスト。

### REQ-OBJECT-001 共通オブジェクト情報

- **価値・詳細:** 文字、フキダシ、付加記号等に安定ID、種類、位置、回転、ZIndex、ロック、親/グループ参照、ページ所属を段階的に導入する。
- **依存:** REQ-SAVE-001。
- **受け入れ条件:** IDが保存・複製・読込で規則通り扱われ、ページ内ZIndexが決定的になる。
- **非対象・影響:** 深い継承階層や全WPF型の抽象化はしない。形式影響大、UIは間接影響。
- **未決・仮定・検証:** UUIDを仮推奨。ADR-0003、シリアライズテスト。

### REQ-UNDO-001 Undo／Redo

- **価値・詳細:** `Ctrl+Z`、`Ctrl+Y`/`Ctrl+Shift+Z`で文字、画像、フキダシ、付加記号、ページ操作を戻す/やり直す。ドラッグは開始から終了を1履歴、連続入力は適切に集約する。
- **依存:** REQ-OBJECT-001、REQ-PAGE-001。
- **受け入れ条件:** 追加・削除・移動・スタイル・ページ操作が可逆で、履歴上限と保存revisionが機能する。
- **非対象・影響:** アプリ再起動後の履歴復元は対象外。UI影響あり、形式影響は原則なし。
- **未決・仮定・検証:** ページ履歴とproject操作履歴の境界はOQ-UNDO-001。コマンド単体・統合テスト。

### REQ-ZORDER-001 重なり順

- **価値・詳細:** 最前面、前面、背面、最背面を提供し、順序を保存する。グループ内部順とフキダシ内部順を決定的にする。
- **依存:** REQ-OBJECT-001、REQ-UNDO-001。
- **受け入れ条件:** 操作・Undo・保存再読込後で同一の描画順になる。同値ZIndexを正規化する。
- **非対象・影響:** 本格レイヤーパネルは対象外。軽量な日本語UI、形式影響あり。
- **未決・仮定・検証:** 連番正規化を仮採用。描画順テストと画像比較候補。

### REQ-LOCK-001 オブジェクトロック

- **価値・詳細:** ロック/解除したオブジェクトは通常の移動・削除・直接選択から保護する。
- **依存:** REQ-OBJECT-001、REQ-UNDO-001。
- **受け入れ条件:** 状態が保存され、Undo可能で、ロック中の禁止操作が一貫する。
- **非対象・影響:** パスワード保護は対象外。UI/形式影響あり。
- **未決・仮定・検証:** ロック中のコンテキスト選択方法はOQ-UI-005。操作テスト。

### REQ-BALLOON-001 基本フキダシ

- **価値・詳細:** 楕円、角丸四角、四角、モノローグ候補の追加、移動、拡縮、塗り、枠線色/幅を提供する。
- **依存:** REQ-OBJECT-001、REQ-ZORDER-001、REQ-UNDO-001。
- **受け入れ条件:** 各形状が編集・保存・再読込・Undoでき、縦横文字を妨げない。
- **非対象・影響:** 思考フキダシは初期形状の調査対象。UI/形式影響大。
- **未決・仮定・検証:** 初期形状確定はOQ-BAL-001。描画・hit test・保存テスト。

### REQ-BALLOON-002 しっぽと文字リンク

- **価値・詳細:** しっぽ追加/削除、先端/根元/幅編集、文字とのリンク、一体移動、グループ単位Z変更を提供する。
- **依存:** REQ-BALLOON-001、REQ-LAYOUT-001。
- **受け入れ条件:** 親参照が保存され、削除時整合性が保たれ、ドラッグ中に応答性を維持する。
- **非対象・影響:** 複数しっぽはCould。UI/形式影響大。
- **未決・仮定・検証:** リンクと一般groupの関係はOQ-BAL-002。統合・性能テスト。

### REQ-LAYOUT-001 フキダシ・背景枠・文字の自動レイアウト

- **価値・詳細:** 横/縦の自動改行、水平/垂直中央、padding、自動縮小、最小文字サイズ、はみ出し警告を提供する。「枠に文字を合わせる」と「文字に枠を合わせる」を明示する。
- **依存:** REQ-BALLOON-001、REQ-NFR-PERF-001。
- **受け入れ条件:** 責任方向が一方向で再帰的拡縮を起こさず、処理後に手動調整できる。
- **非対象・影響:** 高度な禁則・組版エンジン全体は対象外。UI/形式影響あり。
- **未決・仮定・検証:** 改行規則と測定方式はOQ-LAYOUT-001。縦横のgolden scenarioと性能計測。

### REQ-SYMBOL-001 付加記号・付加文字

- **価値・詳細:** 親文字に付属する濁点、半濁点、`!`, `?`, `!?`, `!!`, `?!`、短い任意文字を、em基準offset、scale、rotation、装飾継承付きで扱う。
- **依存:** REQ-OBJECT-001、REQ-NFR-UNICODE-001、REQ-UNDO-001。
- **受け入れ条件:** 親の移動・サイズ・回転へ追従し、個別移動・継承解除・保存・Undoができる。文字間隔へ含めない設定を持つ。
- **非対象・影響:** 無制限のリッチテキスト子要素は対象外。UI/形式影響大。
- **未決・仮定・検証:** 書記素と独自子オブジェクトの併用を仮採用。ADR-0006、フォント別縦横テスト。

### REQ-CLIPBOARD-001 クリップボード画像読込

- **価値・詳細:** 画像を新規ページまたは現在背景として読み込み、既存背景確認、透明画像、サイズ、例外を安全に扱う。
- **取得優先順位:** clipboardに複数形式がある場合は、PNG、DIB、Bitmap／BitmapSourceの順に、有効にdecodeできる最初の画像を採用する。
- **依存:** REQ-PAGE-001、REQ-UNDO-001。
- **受け入れ条件:** 画像なし時は日本語メッセージ、例外時もアプリ継続、置換はUndo可能。
- **非対象・影響:** 完成画像のclipboard書出しは後順位。UI影響あり、形式は背景画像として影響。
- **決定・検証:** OQ-CLIP-001の優先順位をWindows clipboard fixture、STA統合test、alpha・DPI・pixel寸法で検証する。

### REQ-EXPORT-001 ページ画像出力

- **価値・詳細:** 現在ページと全ページの連番一括PNG/JPEG出力を提供する。
- **依存:** REQ-PAGE-001。
- **受け入れ条件:** ページ順、名前衝突、透過、失敗継続方針が明確で、キャンバス寸法通り出力する。
- **非対象・影響:** 動画/PDF出力は対象外。UI影響あり、project形式影響なし。
- **未決・仮定・検証:** ファイル名規則はOQ-EXPORT-001。複数ページ手動・寸法テスト。

### REQ-NFR-PERF-001 操作応答性

- **価値・詳細:** 起動、ページ切替、文字入力、ドラッグを軽快に保ち、Geometryはcacheし、ドラッグ中は必要に応じ簡易表示する。
- **依存:** なし。
- **受け入れ条件:** 基準端末・データセット・計測閾値をTASK-000で定義し、重大な退行がない。
- **非対象・影響:** GPU固有最適化の保証はしない。UI/形式影響なし。
- **未決・仮定・検証:** 数値閾値はOQ-NFR-001。計測とプロファイル。

### REQ-NFR-MEM-001 メモリ上限

- **価値・詳細:** 非表示ページ、複数project、Geometry cache、Undo履歴を上限付きで管理し、閉じたsessionを解放する。
- **依存:** REQ-WORKSPACE-001、REQ-UNDO-001。
- **受け入れ条件:** 定義した代表データで継続増加せず、履歴上限超過時も編集継続できる。
- **非対象・影響:** 巨大画像編集の保証はしない。形式影響なし。
- **未決・仮定・検証:** 履歴件数/byte上限はOQ-UNDO-002。長時間シナリオ計測。

### REQ-NFR-BUILD-001 再現可能ビルド

- **価値・詳細:** 必要SDKを固定・文書化し、Debug/Releaseを1コマンドでビルドできる。
- **依存:** なし。
- **受け入れ条件:** clean環境で双方成功し、警告・成果物を記録する。
- **非対象・影響:** 非Windowsビルドは対象外。UI/形式影響なし。
- **未決・仮定・検証:** .NET 6継続かLTS更新かOQ-BUILD-001。CI/ローカルビルド。

### REQ-NFR-TEST-001 テスト可能性

- **価値・詳細:** 文書モデル、serializer、command、layoutをWPF Window起動なしで自動テスト可能にする。
- **依存:** REQ-NFR-BUILD-001。
- **受け入れ条件:** characterization、互換fixture、状態遷移テストが継続実行できる。
- **非対象・影響:** 全UIの自動E2E化は初期対象外。形式影響なし。
- **未決・仮定・検証:** test frameworkはTASK-000でADR不要の局所判断。test command実行。

### REQ-NFR-ERROR-001 安全なエラー処理

- **価値・詳細:** 読込/保存/clipboard/出力失敗でアプリを終了せず、現在データを保護し、日本語メッセージと診断情報を提供する。
- **依存:** REQ-UI-JA-001、REQ-SAVE-001。
- **受け入れ条件:** fault scenarioで既存ファイルとsessionが残り、詳細ログ失敗も二次例外にしない。
- **非対象・影響:** 遠隔telemetryは対象外。UI影響あり、形式影響なし。
- **未決・仮定・検証:** log保存先はOQ-NFR-002。fault injection。

### REQ-NFR-UNICODE-001 Unicodeと日本語パス

- **価値・詳細:** 書記素クラスター、結合文字、日本語を含むproject/page/text/path/filenameを破損なく保持する。
- **依存:** なし。
- **受け入れ条件:** 指定fixtureのround-tripと縦横描画が成功する。
- **非対象・影響:** 全Unicode縦書き規則の完全保証はしない。形式影響あり。
- **未決・仮定・検証:** grapheme API選択はADR-0006。Unicode fixtureテスト。

### REQ-NFR-DEPS-001 外部依存最小化

- **価値・詳細:** BCL/WPFで実現可能な機能に大型frameworkを追加しない。追加時はライセンス、サイズ、保守性を記録する。
- **依存:** なし。
- **受け入れ条件:** 新規依存に理由とライセンス記録がある。
- **非対象・影響:** 必要なtest-only dependencyは禁止しない。配布影響あり。
- **未決・仮定・検証:** PRレビューとthird-party notice確認。

## 3. Should

### REQ-SELECT-001 複数選択

- **詳細:** `Ctrl+クリック`で独立したtop-level compositionを複数選択し、再クリックで個別解除する。選択状態は保存しない。投げ縄・矩形範囲選択は初期版の対象外とする。複数選択を一体移動、削除、複製、group化の入力として使用し、REQ-OBJECT-001／REQ-UNDO-001に従って縦書き・横書きとfocus競合を検証する。

### REQ-GROUP-001 一般グループ化

- **詳細:** REQ-SELECT-001で選択したcompositionを明示的な一般groupとして保存し、group化／解除、一体移動、Z-order、lock、削除、複製を提供する。初期版はflat groupだけを許可し、nested group、group全体resize／rotation、alignment UIは対象外とする。文字link、非detached付加記号、フキダシlink、`BalloonMergeData`は既存typed compositionを維持し、一般groupへ暗黙変換しない。Undo／Redo、clone、削除、round-trip、invalid relationshipの原子拒否を検証する。

### REQ-OPS-001 基本キーボード操作

- **詳細:** 矢印1px、Shift+矢印10px、Delete、Ctrl+Dを提供する。REQ-UNDO-001依存。フォーカス競合を日本語UIと縦横で確認する。

## 4. Could

### REQ-BALLOON-MERGE-001 非破壊フキダシ合体

- 明示操作で2件以上のフキダシを合体／解除し、各memberのshape、位置、size、rotation、style、tail、text link、layout stateを変更せず保持する。自動合体はしない。
- 合体表示は選択中の基準フキダシをprimaryとし、そのfill／stroke／stroke thicknessをunion外形へ使用する。styleが異なるmemberも拒否せず、解除時には各member固有styleへ完全に戻る。
- body geometryはunionし、overlap部分の内側strokeを表示しない。非overlapは離れた複数領域として安全に表示する。TASK-150では各memberのsingle-tail dataを保持して描画し、TASK-160導入後は全memberの全tailを保持して描画する。
- 合体memberと各memberのlinked text／非detached付加記号をballoon専用typed compositionとして扱い、移動とZ-orderを一体化する。一般group、multi-select、nested groupとは別概念であり、TASK-230は推奨だが必須依存ではない。
- merge／unmerge／group move／Z-orderは1 Undoとし、memberまたはtyped composition内にlocked objectがあれば原子的に拒否する。TASK-160導入後、合体状態のbody resizeはmerge group全体に対する単一操作だけを許可し、member bodyの個別resizeは許可しない。合体解除後は各member bodyを個別resizeできる。TASK-150時点ではtail handle編集も解除後に行うが、TASK-160導入後は合体処理完了後の状態で各memberの各tailを個別選択・編集できる。
- 2.4形式でmerge group ID、primary ID、ordered member IDsを保存する。2.0〜2.3はmergeなしとして読込み、invalid／duplicate／missing memberを持つ2.4 dataは全体を拒否する。
- **依存:** REQ-BALLOON-002、REQ-ZORDER-001、REQ-LOCK-001、REQ-UNDO-001。REQ-GROUP-001は将来統合候補であり必須ではない。

### REQ-BALLOON-TAILS-001 複数しっぽ

- 1フキダシにordered collectionとして0本以上のしっぽを保存し、各tailへstable TailId、先端、根元、幅を保持する。
- UIでtailを個別選択し、追加、先端／根元／幅編集、削除、順序変更を提供する。各semantic操作は1 Undoとし、locked typed compositionでは原子的に拒否する。
- 旧形式のsingle tailは1要素collectionへ移行し、tailなしは空collectionへ移行する。clone、保存再読込、日本語path、未知version、失敗時の元file保護を検証する。
- `BalloonMergeData`に属するフキダシでも、merge操作が正常完了した後はmergeを維持したまま各memberの各tailを個別編集できる。編集結果はunion visualとhit testへ即時反映する。
- 合体状態ではmember bodyの個別resizeを許可せず、merge group全体の外形に対するresizeだけを1操作として提供する。合体解除後は元の各memberへ戻り、各bodyを個別resizeできる。group resize／unmergeはtyped composition全体をlock preflightし、成功時はそれぞれ1 Undoとする。
- merged tailの追加／編集／削除は対象tailだけでなくmerge typed composition全体をlock preflightし、失敗時はmodel、visual、selection、history、dirtyを変更しない。

### REQ-BACKGROUND-FX-001 文字背景ボックスの第2枠線・外側ぼかし

- 対象は文字objectの背景ボックスだけとし、canvas背景色、page背景画像、フキダシには適用しない。
- 通常枠線の外側へ第2枠線を描画し、色、太さ、不透明度、blur／glow半径を設定できる。半径0は硬い第2枠線とし、offset付きshadowは初期版の対象外とする。
- effectは選択範囲とhit testを拡張しないが、canvas内のPNG／JPEG出力へ反映する。設定変更はUndo可能で、保存再読込と縦書き／横書きを検証する。

### REQ-SNAP-001 簡易スナップ

- キャンバス中央、他オブジェクト中央、一時解除を提供する。高度なguide/rulerは対象外。

### REQ-SYMBOL-PRESET-001 フォント別付加記号preset

- 縦横・fontごとの初期offsetを任意に選べる。外部cloud presetは対象外。

## 5. Won't for now

- 投げ縄選択・モザイク、ブラシ型モザイク、本格画像mask、自由描画、高度filter、色調補正、汎用切り抜き。
- Photoshop型レイヤーパネル、複雑な選択範囲、汎用画像編集ソフト化。
- リアルタイム共同編集、cloud同期、非Windows UI、再起動後Undo履歴。

## 6. 要件と主要タスクの追跡

完全な割当は `task-breakdown.md`を原典とする。Must要件は少なくともTASK-000, 005, 010, 020, 025, 030, 040, 041, 050, 060, 070, 080, 090, 100, 110, 120, 130, 140, 170, 190A, 190B, 200へ割り当てる。
