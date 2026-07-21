# α版レビュー・Done方針

Status: Accepted

## 目的

α版では致命的な利用不能やdata安全性の問題を確実に止めつつ、改善余地を理由にreleaseを無期限に保留しない。review severityは実害と再現性に基づいて決める。

## Release-blocking

次の問題は`BLOCKER`またはrelease-blocking `MAJOR`として、修正または明示的なowner判断なしにDone／package生成へ進めない。

- 起動不能、保存・読込不能など、主要な根本動作が成立しない。
- credential漏えい、任意file上書き、path traversal等のsecurity問題。
- 通常の利用経路で再現するcrash、process強制終了、無限loop、hang。
- project、元画像、保存file、Undo対象等のdata破損・消失。
- 上記と同等の重大な実害があり、現実的な回避手段がない問題。

blocking findingには、再現条件、具体的な利用者影響、data安全性への影響、回避不能である理由を記録する。

## Non-blocking technical debt

次は原則として`TECH-DEBT`（既存の3段階分類を使う場合は`MINOR / TECH-DEBT`）に記録し、単独ではα版のDoneやlocal package生成を妨げない。

- 設計上の改善、refactoring候補、より良い実装方法。
- 回避可能な軽微な不具合、UIの粗さ、文言・pixel・操作感の改善。
- manual確認不足、test coverage不足、fixture不足、証拠の過大表現を訂正した後に残る未検証事項。
- 性能最適化候補。ただし実際のhang、memory枯渇、利用不能を再現する場合はblockingへ戻す。

「より良くできる」こと、またはtestが追加できることだけを理由にDone判定を保留しない。既知のsecurity、crash、data破損・消失、無限loop／hangを技術的負債へ降格してはならない。

## α版完了時の扱い

- Debug／Release build、既定の自動test、package内容監査、展開後smoke testを実行し、結果を記録する。
- 実施できないmanual確認と非致命的な既知問題はα版release noteへ明記する。
- 残る技術的負債には後続task候補を付けるが、ownerが追加品質gateを指定しない限りrelease gateにはしない。
- 公開、署名、upload、配布先への登録はlocal package生成とは別のowner decisionとする。
