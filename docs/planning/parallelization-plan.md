# 並行化計画

## 1. 第1陣

共通baseを計画commitとし、競合しない2件だけを選ぶ。

| Task | 選定理由 | 主なfile | Environment |
| --- | --- | --- | --- |
| TASK-000 | 後続全taskのbuild/test不確実性を減らす | solution、新規tests/eng | .NET SDK不在。導入まで実行完了不可 |
| TASK-190A | product codeとsolutionを触らず、非公式表記・license riskを先に解消 | README、新規notice | SDK不要 |

TASK-000とTASK-190Aは同じ主要fileを変更せず、保存形式・MainWindow・XAMLへ触れない。TASK-190Aは即開始可能、TASK-000はworktreeと指示書を準備するがSDK blockerを隠さない。

## 2. 除外した高優先task

| Task | 除外理由 |
| --- | --- |
| TASK-005 | TASK-000のfixture/test harnessに依存し、DataIO/MainWindowを変更する |
| TASK-010 | TASK-000に依存。domain APIのtest基準が先 |
| TASK-020 | TASK-010依存かつMainWindow最大hotspot |
| TASK-030 | 020/025/041依存。tabだけ先行すると状態が再びUIへ埋まる |
| TASK-040 | safe writerとdocument modelの両方に依存 |
| TASK-060 | project/page container決定後でなければID scopeを確定できない |
| TASK-070 | editor boundaryとobject identityが必要 |
| TASK-110 | object/Undo/serializerへ触れるため直列化 |

## 3. File競合matrix

| File/Area | TASK-000 | TASK-190A |
| --- | ---: | ---: |
| Solution/csproj | Write | None |
| tests/eng | Create | None |
| README | None | Write |
| THIRD-PARTY-NOTICES | None | Create |
| CHANGELOG | Avoid（reportのみ） | Write |
| Product C#/XAML | None | None |
| Planning docs | 自task ledger/report行のみ | 自task ledger/report行のみ |

## 4. Merge順

1. TASK-190A（docs-onlyで低risk）。
2. TASK-000（solution/test baselineを全後続のbaseにする）。
3. integration後、Debug/Release/testとlicense/README linkを再確認。

TASK-000が先に完成しても単独で統合可能だが、第2陣のbaseはTASK-000統合後とする。

## 5. 第2陣開始条件

- 必要SDKが利用可能でDebug/Release/test commandが成功する。
- TASK-000が統合され、legacy fixtureの原本hashが固定される。
- TASK-190Aのlicense調査でproduct distributionを止めるblockerがない。
- その後、TASK-005とTASK-010は主fileが異なる範囲を再確認して2並行候補とする。solution/test common fileへの同時編集は禁止する。

## 6. 失敗時の代替

- SDKを準備できない場合、TASK-000はBlockedとし、未検証のproject設定をcommitしない。TASK-190Aのみ統合可能。
- test framework導入が不適切なら、最小console characterization harnessを比較し、決定をreportへ残す。
- 第三者codeの由来を確定できない場合、推測のnoticeを追加せず、該当fileと候補source、必要な専門確認を報告する。

## 7. TASK-030統合後の現在wave

基準HEADは `7fc27d5f454854ee0812f5267da7fe662485bc03`。依存DAG上は次の3taskが開始可能である。

| Task | 依存状態 | 主な変更領域 | 並行判断 |
| --- | --- | --- | --- |
| TASK-060 | TASK-010統合済み | document/object model、serializer、tests。MainWindow変更禁止 | UI task 1件と安全に並行可能。P0基盤のため必須lane |
| TASK-050 | TASK-030統合済み | export service、MainWindow/PageEditorの出力UI | TASK-060と並行可能。TASK-190BとはMainWindow/XAML競合のため不可 |
| TASK-190B | TASK-020、TASK-190A統合済み | MainWindow/App/About、assembly/package metadata、license導線 | TASK-060と並行可能。TASK-050とは直列化 |

推奨waveは `TASK-060 + TASK-190B`。TASK-190Bは比較的小さく、未完了の配布時license/非公式表記を先に閉じられる。TASK-190B統合後、TASK-060が継続中でもTASK-050を開始できる。

代替waveは `TASK-060 + TASK-050`。ユーザー価値として全ページ出力を優先する場合はこちらを選ぶ。いずれもTASK-050とTASK-190Bを同時開始しない。

TASK-070はTASK-060完了後に依存解消するが、MainWindow/PageEditorを変更するため、その時点で進行中のTASK-050またはTASK-190Bとの競合を再判定する。
