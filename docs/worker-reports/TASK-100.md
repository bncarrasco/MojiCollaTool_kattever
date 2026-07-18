# TASK-100 付加記号UI・書記素描画 実装報告書

## 1. 基本情報

- Task: `TASK-100`
- Branch: `feature/TASK-100-attached-symbol-ui`
- Worktree: `F:/github/MojiCollaTool-worktrees/TASK-100`
- SDK: `C:\Users\user\.dotnet\dotnet.exe` / `6.0.428`
- 開始時Git確認: top-levelは上記worktree、branchは上記feature branch、開始HEADは`8e9ae8ee860690d11090fc055f1ba055c15969dd`、開始時statusはclean、worktree登録済み。
- 完全実装コミット: `864a95d76b5d34f7ac491e619464724908ad13d0`
- 追加受入実装・最終実装コミット: `6dc0f1f00b40be7b8f29057cb3d18d2c74abd15c`

## 2. 実装内容

- detached付加記号をvisual一覧とは別のモデル一覧で保持し、親削除、Capture、ページ／プロジェクト切替、保存・再読込で保持。
- Capture前にcloneしたPageDocumentへ全状態を投入して検証し、成功後だけ文書を更新。追加時もText、ID、parent、anchor、scale、offset、rotation、finite値をvisual変更前に検証。
- 空文字、256文字超、範囲外anchor、重複IDでUI・文書・履歴が変わらない原子性を確保。
- dragごとに一意のcoalesce keyを発行し、2回の短時間dragを2履歴へ分離。capture lossは開始状態へ復元。
- `IsVisible=false`およびdetached visualは描画geometryを生成せず、非表示・非hit-test状態に変更。
- 濁点、半濁点、「装飾継承」、「文字間隔に含める」を追加。フォント候補は固定リストではなく、既存FontUtilの全system font列挙を使用。
- 設定領域を実際の`ScrollViewer`内へ配置。
- grapheme全体をキーとするvisual poolへ更新。FontUtilの全system font列挙結果はLazy cacheし、列挙内容は変更していない。

## 3. 変更ファイル

- `MojiCollaTool/MojiCollaTool/PageEditorControl.xaml.cs`
- `MojiCollaTool/MojiCollaTool/Visuals/AttachedSymbolVisual.cs`
- `MojiCollaTool/MojiCollaTool/MojiPanel.cs`
- `MojiCollaTool/MojiCollaTool/DecoratedCharacterControl.cs`
- `MojiCollaTool/MojiCollaTool/DecoratedCharacterControlPool.cs`
- `MojiCollaTool/MojiCollaTool/DecoratedCharacterControlTotalPool.cs`
- `MojiCollaTool/MojiCollaTool/MojiWindow.xaml`
- `MojiCollaTool/MojiCollaTool/MojiWindow.xaml.cs`
- `MojiCollaTool/MojiCollaTool/FontUtil.cs`
- `tests/MojiCollaTool.Tests/TASK100AttachedSymbolUiTests.cs`
- `docs/worker-reports/TASK-100.md`
- `docs/testing/verification-matrix.md`
- `docs/planning/implementation-ledger.md`
- `CHANGELOG.md`

## 4. 自動検証範囲

`TASK100AttachedSymbolUiTests`は14件です。

- variation selector、skin tone、ZWJ、国旗2個、CRLF、空行。
- 横書き／縦書き、2種類のWPF認識済みsystem font、有限な配置、offset調整、親の移動・サイズ・回転追従。
- 全候補記号、任意文字、font継承／解除／fallback／保存名、decoration、spacing。
- visual階層を含むmixed canonical Z-order。
- add、remove、property、dragのUI controller経路におけるUndo／Redo、saved dirty、redo branch破棄。
- 2回drag＝2 Undo、capture loss、page/project切替、selection復元、Dispose。
- detached保持、親削除、保存再読込、`IsVisible=false`。
- atomic add失敗、grapheme refresh、複数付加記号refresh／drag性能。

既存TASK-090テストではモデル層のorphan policy、cross-object ID、persistence compatibility、model atomicityを検証しています。

## 5. 性能実測値

Debug実行時、24個の付加記号を配置し、同一parentをrefresh／dragしました。

- refresh 50回: **471.163 ms**
- drag 25回: **11.315 ms**
- 計測テストの閾値: 各5,000 ms未満

FontUtilの全system font列挙は初回のみ実行し、以後はLazy cacheを使用します。

## 6. 検証結果

- `C:\Users\user\.dotnet\dotnet.exe --version`: pass、`6.0.428`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Debug`: pass、0 warnings / 0 errors
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\test.ps1`: pass、169 passed / 0 failed
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\build.ps1 -Configuration Release`: pass、0 warnings / 0 errors
- `C:\Users\user\.dotnet\dotnet.exe test .\tests\MojiCollaTool.Tests\MojiCollaTool.Tests.csproj -c Release --no-build`: pass、169 passed / 0 failed
- `git diff --check`: pass
- 最終status: clean

## 7. 既知問題・未検証範囲

- 実機WPFでのマウス押下、pointer capture、DPI倍率、IME入力、最終pixel表示は自動テストだけでは代替できず、手動確認が必要。
- フォントはテスト環境で認識された2種類を自動検証しているが、全インストールフォントのglyph差異やフォント別pixel比較は未実施。
- 性能値は本環境の単一実行値であり、他PCのGPU、DPI、フォント数による差異は未評価。

## 8. ロールバック

今回の追加受入実装だけを戻す場合は、`git revert 6dc0f1f00b40be7b8f29057cb3d18d2c74abd15c`を使用する。TASK-100全体を戻す場合は、後続の報告・台帳コミットを含め、対象コミットを新しい順に`git revert`する。`reset --hard`、push、merge、rebaseは使用していない。
