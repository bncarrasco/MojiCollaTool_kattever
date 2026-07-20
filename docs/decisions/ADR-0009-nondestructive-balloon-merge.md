# ADR-0009: 非破壊フキダシ合体をballoon専用compositionとして保存する

- **Status:** Accepted
- **Context:** TASK-150では複数のフキダシbodyを一つの外形として表示し、overlap部分の内側strokeを隠す必要がある。一方で解除後に元shape、位置、style、tail、text link、layout stateを完全復元し、TASK-230の一般groupと混同してはならない。
- **Decision:** page-levelの`BalloonMergeData`を導入し、stable merge ID、primary balloon ID、canonical orderを持つ2件以上のmember balloon IDを保存する。memberの`BalloonData`自体は合体操作で変更しない。
- **Primary/style:** UIで先に選択されたballoonをprimaryとし、合体表示のfill、stroke、stroke thicknessはprimary値を使用する。member styleは保持し、unmerge後に再表示する。style不一致を理由に合体を拒否しない。
- **Geometry:** member bodyをpage座標へ変換してunionし、body fillと外周strokeを一度だけ描画する。各memberのsingle tailは保持して合成表示する。non-overlapは複数の離れた領域として正常状態とする。geometry/cache keyはordered member IDとshape／bounds／position／rotationを含む。
- **Composition:** merge memberごとの既存`balloon + linked text + non-detached attached symbols`を結合し、一つのtyped Z-order blockとして扱う。merge group moveは全member compositionを同じdeltaで移動する。合体中の個別resizeとtail handle編集は無効化し、解除後に行う。
- **Lock/atomicity:** merge、unmerge、group move、group Z-orderは全member compositionをpreflightし、locked／missing／duplicate／invalid relationshipがあればmodel、live visual、selection、history、dirty、status notificationを部分変更せず拒否する。成功は1 Undo entryとする。
- **UI:** selected primaryと候補balloonをpairwiseに「フキダシ合体」し、既存groupへ追加できる。「合体解除」はgroup全体を解除する。自動合体、multi-select、一般group、nested mergeは実装しない。状態・拒否理由は日本語で表示する。
- **Persistence:** project formatを2.4へ上げ、2.4 writer／minimum readerでmerge dataをround-tripする。2.0〜2.3はmerge groupなしとして読む。2.4のempty ID、2件未満、primary不在、missing／duplicate member、同一balloonの複数group所属はload前validationで拒否する。2.5以降、unknown major、future minimum readerは従来どおり拒否する。
- **Clone/delete:** page identityを保持するcloneはmerge ID／member IDを保持し、新規page cloneはmember IDとmerge IDを再生成する。member削除はmerge relationshipを更新し、2件未満になればgroupを削除する。ただしlocked related memberがある非force削除は拒否する。
- **Performance:** Releaseで24 groupのrefresh/hit-testを測定し、single operation p95 50 ms未満、100 refresh batch 1,500 ms未満を受入目標とする。Debug safety thresholdは3,000 msとする。
- **Implementation evidence:** TASK-150 `0f9a657c89159f94f42a61f04e87d507cf040afc`で実装し、Debug/Release 246/246、専用8/8に合格した。Release実測はsingle p95 0.045 ms、24 group×100 batch 112.663 ms。実WPF pointer／DPI／IME／final-pixel目視はNot verified。
- **Consequences:** 2.4で保存したprojectは2.3以前のreaderでは開けない。旧project読込と元fileを保護するatomic writerは維持される。TASK-230は将来、selection UIを再利用できるが、merge dataやtyped compositionを一般`GroupId`へ暗黙変換しない。
- **Alternatives rejected:** 焼き込みGeometryは元parameterを失う。既存`IPageObjectData.GroupId`の流用は一般groupとの意味衝突と現行relationship修復の対象になる。style一致だけを許可する案は不要に機能を狭める。
- **Related:** REQ-BALLOON-MERGE-001、ADR-0003/4/5/7/8、TASK-080/130/140/150/230。
