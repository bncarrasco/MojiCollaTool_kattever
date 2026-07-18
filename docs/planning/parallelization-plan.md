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

## 7. TASK-060／TASK-190B統合後の現在wave

TASK-060とTASK-190Bは`develop`へ統合済み。依存DAG上は次の3taskが開始可能である。

| Task | 依存状態 | 主な変更領域 | 並行判断 |
| --- | --- | --- | --- |
| TASK-050 | TASK-030統合済み | export service、MainWindow/PageEditorの出力UI | TASK-070およびTASK-200と競合するため単独lane |
| TASK-070 | TASK-020、TASK-060統合済み | history service、PageEditor input、MainWindow command bindings、tests | P0。TASK-050およびTASK-200と競合するため次の単独laneに推奨 |
| TASK-200 | TASK-190B統合済み | XAML/CS全般の日本語UI inventoryと修正 | TASK-050およびTASK-070と競合するため単独lane |

現在のtask粒度では安全に並行実装できる組み合わせはない。優先度と後続解放数から、次はTASK-070を単独で実施する。

TASK-070は`develop`へ統合済みであり、TASK-080、TASK-090、TASK-110、TASK-170、TASK-210、TASK-230の依存が解消した。

## 8. TASK-070統合後wave

次waveはTASK-050とTASK-110を並行実施する。

| Task | 所有領域 | 変更禁止領域 | 並行判断 |
| --- | --- | --- | --- |
| TASK-050 | export service、出力dialog、MainWindow/PageEditorの出力command、export tests | balloon model、relationship DTO、project XML schema | 既存UI/export laneとして実施 |
| TASK-110 | 新規balloon model、relationship DTO、versioned serializer、history command、model/serializer tests | MainWindow、PageEditor、XAML、export service | model/schema laneとして実施 |

両taskは同一の`develop`開始commitからworktreeを作成する。共有台帳・CHANGELOG以外の横断fileを同時変更せず、範囲外変更が必要になった場合は実装前に司令役へ報告する。TASK-050はTASK-200と、TASK-110はTASK-090/TASK-230と同時開始しない。

本waveは完了し、TASK-110を`fd307beec9d0cf6c8865f219a99ca44ce2353733`、TASK-050を`726ba07e2dcfff0fe4e43e929a24731de3c6059f`で`develop`へ統合した。統合状態のDebug/Release buildは警告0・エラー0、全testは各124/124成功。

## 9. TASK-050／TASK-110統合後wave

次waveはTASK-090とTASK-120を並行実施する。両taskは後続TASK-100とTASK-130をそれぞれ解放し、model/schema laneとPageEditor/render laneへ分離できる。

| Task | 所有領域 | 変更禁止領域 | 並行判断 |
| --- | --- | --- | --- |
| TASK-090 | grapheme service、AttachedSymbol model、MojiDataの必要最小限拡張、versioned serializer、model/history/Unicode tests | PageEditor、MainWindow、balloon visual/Geometry、フキダシUI | Unicode/model/schema laneとして実施 |
| TASK-120 | balloon Geometry factory/cache、balloon visual、PageEditorの追加・選択・move/resize、必要最小限のMainWindow command、render/performance tests | MojiData、AttachedSymbol、VersionedProjectFormat、project schema | balloon render/UI laneとして実施 |

共有の`PageDocument`とhistory基盤は既存APIを利用し、一般refactorを行わない。範囲外変更が必要な場合は実装前に司令役へ報告する。TASK-090はTASK-100/TASK-230と、TASK-120はTASK-080/TASK-170/TASK-200/TASK-210と同時開始しない。
