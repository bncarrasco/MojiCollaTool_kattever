# TASK-030 ワーカー指示書

## 役割と作業場所

あなたはプロジェクト／ページタブUI統合担当です。既存の文書モデル、workspace、共通reader、versioned writer、`PageEditorControl`を接続し、同一アプリ内で複数プロジェクトと複数ページを安全に扱えるshellを実装します。

- Branch: `feature/TASK-030-project-page-tabs`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-030`
- Functional base: `8875af4696663f34a6ba72f255b2f9e209578dd2`
- 開始時HEAD: 司令役のkickoff指示に記載するexact hash
- 統合済み依存: TASK-020、TASK-025、TASK-040、TASK-041
- 最初に `AGENTS.md`、`docs/worker-prompts/README.md`、本指示書、ADR-0001、ADR-0002、`requirements.md`のREQ-PAGE/WORKSPACE/DIRTY/UI-JAを読む。

編集前に共通preflightを実行し、top-level、branch、HEAD、clean status、worktree登録が1項目でも不一致なら作業を開始しない。directoryコピー、`.git`コピー、`git init`による代用は禁止する。

## 目的・要件

- REQ-PAGE-001
- REQ-WORKSPACE-001
- REQ-DIRTY-001
- REQ-UI-JA-001
- ADR-0001、ADR-0002

上段にプロジェクトタブ、下段に選択中プロジェクトのページタブを表示する。Chrome型の単一バー／折りたたみグループ、複数Window、プロジェクト間ページ移動は初期対象外とする。

## 固定するUX

- 起動時は「無題」の新規プロジェクト1件、ページ`01`を表示する。
- 「新規作成」は現在の内容を破棄せず、新しいプロジェクトタブを追加する。
- プロジェクトタブはproject名、未保存時は末尾`*`、閉じる操作を表示する。
- ページタブはpage名を入力どおり表示し、変更されたページには`*`を表示する。保存成功時に当該project配下の印を消す。
- ページ操作は追加、複製、削除、名前変更、左へ移動、右へ移動を日本語で提供する。最後の1ページは削除できず、日本語で通知する。
- ページ初期名は既存modelの`01`, `02`形式を使う。rename時にユーザー入力を勝手にtrim、正規化、翻訳しない。
- project/page切替時はactive session/pageを同期し、active pageだけを`PageEditorControl`へ表示する。非active pageごとにWPF controlや編集Windowを常駐させない。
- dirty projectを閉じる時は「保存」「保存せず閉じる」「キャンセル」の3択とし、対象projectだけへ適用する。アプリ終了時は全sessionを安全に確認する。
- 同一pathを開こうとした場合は既存タブをactiveにし、日本語で通知する。2件目のsessionは作らない。
- legacy形式を開いた場合はT041の日本語migration warningを表示する。次の通常保存はversioned形式とする。

## 実装上の必須境界

### Workspaceとeditor

- `MainWindow`をworkspace shellとし、`ApplicationWorkspace`／`ProjectSession`を状態の正とする。タブだけの別状態を作らない。
- page追加・複製・削除・rename・reorderと、文字／画像／canvas変更はsessionの変更経路を通し、revisionとdirty表示を更新する。
- `PageEditorControl`へ必要最小限のpage bind/unbindまたは変更通知を追加してよい。全面MVVM化、`MainWindow`複製、pageごとのWindow常駐は行わない。
- page切替前にeditorの未反映状態をdocumentへ反映し、切替後に別pageのCanvasData、MojiData、画像が混入しないことを保証する。
- page/projectを閉じた時は`MojiWindow`、`CanvasEditWindow`、画像stream、event購読、temp assetを解放する。

### 画像asset隔離

- 現在のglobal `Working` directoryを複数session/pageのasset正本として共有しない。
- session単位かつpage ID単位で画像1/2を隔離する一時asset storeを設け、`IProjectAssetSource`、`IProjectAssetSink`、`IProjectAssetBatchSink`へ接続する。新規外部依存は追加しない。
- 複数assetの読込は全検証後に一括反映し、失敗時に既存sessionや別sessionを変更しない。
- page複製時は画像bytesも新PageIdへ複製し、page削除／project close時は該当assetを解放する。
- 画像入替、2枚目追加、D&Dはactive session/pageのassetだけを更新する。

### 読込・保存transaction

- 読込は一時asset storeへ `DataIO.ReadProject(path, sink)`を完了してからworkspaceへsessionを追加する。失敗時は既存タブ、active tab、assetを変更しない。
- 保存はactive editorの状態を反映後、`DataIO.WriteVersionedProject(path, document, assetSource)`を使う。成功後だけFilePathを設定して`MarkSaved`する。
- 名前を付けて保存するpathが別sessionですでに開かれている場合は拒否し、既存file/sessionを保護する。
- 保存失敗やダイアログcancelではFilePath、saved revision、dirty、既存fileを変更しない。
- 旧形式専用writerやglobal Working依存のUI経路へ戻さない。

## Scope

- `MainWindow.xaml`／`.xaml.cs`のworkspace shell化
- project/page tab表示と日本語command／dialog
- `PageEditorControl`の最小限のpage接続・変更通知
- session/page asset storeとライフサイクル
- workspace/session APIの不足を埋める最小変更
- T030自動テスト、UI/manual確認記録
- `CHANGELOG.md`、T030報告書、台帳のTASK-030行

## Scope外

- Chrome型tab group、tab group折りたたみ
- project間page移動、project tabの高度なdrag reorder
- Undo/Redo本体、全ページ一括出力、clipboard機能
- 保存schema変更、公式版互換export
- アプリbranding／既存英語UI全体の一括修正
- 汎用MVVM framework、新規外部依存、全面書き換え

## 受入テスト

最低限、WPF Windowを起動しないstate/service testと必要なSTA integration testで次を固定する。

1. 2project×3pageを作り、active project/page、名前、順序、CanvasData、MojiData、画像1/2、dirtyが混在しない。
2. page追加、複製、削除、rename、左右移動後の順序とactive pageが正しい。複製画像は別PageIdで保存できる。
3. project/page切替を反復しても文字panel、画像、編集Window参照が前pageから残らない。
4. 同一fileの相対表記／絶対表記／大文字小文字差を二重openできず、既存sessionが維持される。
5. versioned 3page projectを保存・再読込し、page ID、順序、Unicode名、縦書き／横書き文字、画像assetを保持する。
6. legacy projectを1pageとして開き、warningを返し、versioned保存後に再読込できる。
7. 保存成功時だけdirtyが消え、保存失敗／cancelではdirty、FilePath、既存fileが維持される。
8. dirtyな一方のprojectをclose cancelしても他projectへ影響せず、discard/save選択時だけ対象sessionを閉じる。
9. page/project close後にasset storeとpage editor関連参照が解放される。

## 手動確認

- 上段project tab／下段page tabが区別でき、2project×3pageを切り替えられる。
- 新規UI、tab、dialog、error、tooltip、状態表示に意図しない英語がない。
- 縦書きpageと横書きpageを交互に切り替え、本文・配置・画像が混ざらない。
- 日本語project/page名、日本語path、Unicode／emoji本文を保持する。
- rename入力、文字入力、ショートカット操作中にtab切替が誤発火せず、keyboard focusが破綻しない。
- 画像追加、D&D、保存、再読込、個別close、アプリ終了確認を実施する。
- 実施できない項目は成功扱いせず、理由付き`Not verified`とする。

## 検証command

```powershell
powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Debug
powershell.exe -ExecutionPolicy Bypass -File eng\build.ps1 -Configuration Release
powershell.exe -ExecutionPolicy Bypass -File eng\test.ps1
git diff --check
```

開始時baselineは60 tests。既存60件を維持し、追加件数と総数を報告する。

## 完了処理

- `docs/worker-reports/TASK-030.md`を作成する。
- `docs/planning/implementation-ledger.md`はTASK-030行だけを更新する。
- `CHANGELOG.md`はユーザーに意味のある変更だけを記録する。
- 報告書に開始時Git確認、Base/Result commit、branch、worktree、変更file、設計判断、要件ID、build/test、手動確認、日本語UI、縦横、日本語path、互換性、既知risk、後続TASK-050/TASK-170、merge注意、rollbackを記載する。
- 英語commit messageで自branchへcommitする。push、merge、rebase、履歴改変は行わない。
