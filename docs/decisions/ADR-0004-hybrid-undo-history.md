# ADR-0004: CommandとMementoの混合Undo

- **Status:** Proposed
- **Context:** UIがpropertyを直接変更し、drag moveごとに座標を更新する。全snapshotは画像・Geometryを含めると重い。
- **選択肢:** Command、全体Memento、property差分、event sourcing、混合。
- **Decision:** semantic Commandと小さいbefore/after Mementoを混合し、previewとcommitを分離する。
- **理由・利点:** page操作とobject操作を表現でき、dragを1履歴へ集約しやすい。
- **欠点・副作用:** 全mutation経路をcommand境界へ寄せる必要があり、実装規律が必要。
- **UI影響:** Ctrl+Z、Ctrl+Y/Ctrl+Shift+Z、操作名の日本語表示。
- **互換性影響:** 履歴は保存しないためproject形式への直接影響なし。dirty revisionに影響。
- **関連:** REQ-UNDO-001, REQ-DIRTY-001, TASK-070。
- **移行:** position/add/deleteから開始し、style/page/layoutへ拡張。
- **見直し条件:** memory benchmarkまたはpage/project履歴prototypeが不適切と示した場合。
- **Supersedes / Superseded by:** なし。
