# Two-era chapter narrative

Authorization: the human explicitly requested implementation on 2026-10-30, covering the existing two eras and all 24 upgrades, with logic/save verification and no APK or visual work. The untracked `research/narrative-v01/` proposal remains preserved reference material. Its one-house ending, old prices and assumed purchase order are superseded here.

Implementation and scoped Editor acceptance are complete. Pure content producer `d98b231` and the final Runtime retention/capture revision `8615038` were independently reviewed before primary application. The authoritative source hashes, receipts, earlier-source reuse and preservation limits are in `docs/evidence/unity/narrative-v02-20261030/verification.json`.

Primary implementation is `7090ff4`, with original producer stdout/stderr retained in `1ea7530`. Independent final read-only review verified all 36 artifact identities, five final source hashes, the 12 producer additions, exact narrow-source test reuse and ordinary snapshot differences. Coordinator also matched the five advertised final source files with zero mismatches. Review ran no additional tests, Editor actions or builds.

## Scope and arc

Potato finds a neglected yard; Pigeon appoints itself its supervisor. In Junkyard they make found things useful and earn a home. Workshop Yard turns the same six families into a place where useful work continues. At chapter completion the place has earned the name Neon; text must not claim that an unimplemented neon sign, city or new era has appeared. The two existing postchapter orders are optional help with sorting and deliveries, not new missions or reward systems.

All six families can develop in any legal order. Each completion reaction must describe only its own purchased stage. Global claims about all six buildings belong only to the existing era-ready or chapter-complete facts. House level2 does not end the chapter. Selection can buy immediately; spins and resumed pending operations use the same existing recorded completion. No story scene, popup, extra delay, forced target or automatic purchase is introduced.

## Content contract

Use pure immutable `JunkToNeon.Guidance.ChapterNarrative` content with stable project IDs. Suggested shared interface: static `Opening`, `EraReady`, `EraEntry`, `ChapterEnd`; `TryCompletion(string projectId, out NarrativeBeat beat)` with immutable `ProjectId`, `Title`, `Body`; `OrderIntroduction(string orderId)` returning null for unknown IDs. No Unity dependency, state mutation, reward, clock, random draw or IO. Unknown completion IDs retain the existing factual fallback.

Russian strings below are literal proposed product copy authorized for this implementation; working documentation remains English. A producer may shorten copy for existing label bounds without changing its meaning. Functional effect descriptions, prices, selection status and safety/recovery instructions remain authoritative and separate from narrative copy.

| Moment | Literal narrative text | Existing fact/placement |
|---|---|---|
| Opening | Картошка нашла забытый двор. Голубь объявил себя главным. Начните с ремонта дома. | Existing Fresh guidance; no new grant or intro replay for Existing saves |
| Era ready | Во дворе уже есть всё для работы. Картошка предлагает открыть Мастерской двор. Голубь соглашается руководить. | All six Junkyard families at local2; append existing free-transition explanation |
| Era entry | Вещи получили второй шанс. Теперь Картошка и Голубь обустраивают Мастерской двор. | Existing committed transition/acknowledgement panel; skip/reload semantics unchanged |
| Chapter end | Из забытых вещей вырос свой двор. Картошка дала ему имя «Неон». Голубь назначил себя почётным соседом. Глава завершена. | All six Workshop Yard families at local2 only; append honest optional-orders/remaining-skills explanation |
| Sort order | Картошка разбирает находки: полезным вещам ещё найдётся дело. | Existing sort order; append actual target, reward, new-spins, claim/cancel rules |
| Delivery order | Голубь взялся помогать соседям. На этот раз важны доставки, а не громкие обещания. | Existing delivery order; append actual three-completed-deliveries conditions and fixed reward |

## All 24 completion reactions

| Project ID | Short title | Body |
|---|---|---|
| `house.repair` | У Картошки появилась крыша. | Голубь недоволен: дыру он считал отдельным входом. Теперь можно обживать двор. |
| `house.improve` | Дома стало просторнее. | Картошка устроилась поудобнее. Голубь записал расширение на свой счёт. |
| `workshop.build` | Нашлось место для дела. | Картошка собрала рабочий уголок. Голубь назвал себя начальником мастерской. |
| `workshop.improve` | Работа идёт спорее. | У каждого инструмента теперь своё место. Кроме того, который снова унёс Голубь. |
| `recycler.build` | У находок появился порядок. | Картошка отделяет полезное от совсем бесполезного. Голубь проверяет каждую блестящую вещь. |
| `recycler.improve` | Меньше хлама, больше пользы. | Разбирать находки стало удобнее. Голубь настаивает, что коллекцию крышек трогать нельзя. |
| `pigeon-loft.build` | Голубь получил свой адрес. | Он немедленно объявил его главным адресом двора. Теперь есть откуда отправляться с доставкой. |
| `pigeon-loft.improve` | Голубь расширил владения. | Места стало больше. По версии Голубя, это уже представительство с приёмной. |
| `cart.build` | У находок появились колёса. | Картошка больше не тащит всё по одной вещи. Голубь предпочитает ехать сверху. |
| `cart.improve` | Телега готова к большему. | Перевозить находки стало удобнее. Голубь добавил в должность слово «логистика». |
| `power-source.build` | Двору пригодилась энергия. | Картошка собрала источник питания из найденных деталей. Голубь требует не называть его случайностью. |
| `power-source.improve` | Источник стал надёжнее. | Картошка довела сборку до ума. Голубь назначил себя ответственным за важный вид. |
| `house.workshop-yard.upgrade-1` | Дом становится своим. | Картошка обустраивает место для жизни. Голубь оставляет за собой право заглядывать без стука. |
| `house.workshop-yard.upgrade-2` | Дом готов к обычной жизни. | Здесь можно оставаться, а не пережидать. Голубь считает это своей главной заслугой. |
| `workshop.workshop-yard.upgrade-1` | Мастерская набирает ход. | Картошка устраивает работу удобнее. Голубь проводит совещание с единственным участником — собой. |
| `workshop.workshop-yard.upgrade-2` | Мастерская знает своё дело. | У найденных вещей теперь есть шанс снова пригодиться. Голубь принимает похвалу за всех. |
| `recycler.workshop-yard.upgrade-1` | Находки идут в дело. | Картошка улучшает сортировку. Голубь наконец согласился отличать запас деталей от своей коллекции. |
| `recycler.workshop-yard.upgrade-2` | Порядок стал привычкой. | Полезные вещи больше не теряются в куче. Голубь делает исключение только для любимой крышки. |
| `pigeon-loft.workshop-yard.upgrade-1` | У Голубя большие планы. | Он обустраивает своё место и составляет список важных вылетов. Картошка просит начать с полезных. |
| `pigeon-loft.workshop-yard.upgrade-2` | Главный адрес обустроен. | Голубь доволен владениями. Для доставки теперь остаётся главное — действительно летать. |
| `cart.workshop-yard.upgrade-1` | Телега держит темп. | Картошка готовит её к новым находкам. Голубь упражняется в руководстве погрузкой. |
| `cart.workshop-yard.upgrade-2` | Каждой находке найдётся путь. | Телега обустроена как следует. Голубь по-прежнему уверен, что самое ценное везут сверху. |
| `power-source.workshop-yard.upgrade-1` | Энергия работает на двор. | Картошка улучшает источник питания. Голубь предлагает назвать его своим именем. |
| `power-source.workshop-yard.upgrade-2` | У энергии есть своё место. | Сборка закончена. Картошка довольна делом, Голубь — тем, что снова присутствовал при успехе. |

## Runtime and persistence invariants

- Derive text from the existing valid chapter/guidance/order facts. Do not add save fields, schema/rule/price revisions, history lists, story rewards or persistent replay queues.
- Use existing latest-completion identity and exact acknowledgement. Acknowledging an older completion cannot hide a newer one. Existing snapshots gain no synthetic past events or duplicated grant; loading and looking at text write nothing.
- Reuse current optional guidance controls and existing transition/order panels. Never stop AUTO, open a panel, advance a clock or hold a reel just to narrate. No scene/layout/font/art/audio changes.
- Keep the latest completion reaction available under AUTO through the existing retained guidance fact and manual hint inspection. Story text must not rely solely on a transient 0.25second message.
- Retain only a completion actually observed on an uncovered idle Canvas. Busy acquisition may keep the prior observed reaction, but idle reload/backup replacement must invalidate a retained identity that differs from the current completion; a covered new snapshot must never inherit another save's reaction.
- Capture that immutable reaction once per identity, using idle invalidation to permit recapture. The new retention path must not repeatedly project serialized guidance on every idle frame.
- Preserve economic effect/price copy, explicit target/reselection, old600/540 pending/paid/replay terms and skillV1/V2/V3 ownership. Recovery, corrupt-save, overflow, pause, offline-income and Back copy keep their existing meaning.
- At both terminal states, never claim an unbuilt family is finished. Final completion must not repeat the obsolete one-house ending or promise a third era. Optional postchapter orders keep exact target/reward/claim/cancel behavior.

## Observable acceptance

Pure checks independently enumerate the literal24 IDs and both global chapter boundaries, compare exact before/after encoded snapshots during narrative reads, and cover legacy/current/pending saves without eager writes. Behavioral cases cover at least two legal family orders, completion by spin and affordable selection, old frozen-price recovery, stale completion acknowledgement, unseen transition reload/skip and final optional orders.

The primary owner runs affected Core checks, clean Unity compilation/EditMode and focused private UI cases with real existing controls: opening, representative early/late/long completion copy, immediate purchase, AUTO/latest completion, reload, era boundary and final state. Verify displayed functional benefit and existing text bounds without redesign. Freeze source for independent review before primary integration; restore the latest real ordinary save and portrait preview afterward. No APK or device action belongs to this scope.

## Verified implementation outcome

The pure producer passed five groups, two funded legal24-purchase orders, two actual fresh spins and138 exact narrative-read snapshot checks, including genuine historical inputs and frozen600/540 terms. Initial primary integration passed Core49, compilation0errors/0warnings, EditMode11 and nine focused UI cases. Subsequent review found a transient cross-snapshot reaction risk; a same-sequence/different-project covered Reload case verifies that old text is discarded and new text waits for presentation. Final capture-once cleanup removes redundant new Canvas projections without a measured speedup claim. Each narrow Runtime revision was recompiled and its affected extended NarrativeAuto case passed; unchanged prior cases retain their exact source qualification rather than being claimed as a clean final-revision nine-case rerun.

Both save format and economic/persistence/rule source remain unchanged. Latest ordinary43coins/seed1118863915/lastSpin138 were preserved; actual Reload advanced only clock/revision/sequence metadata (142->158), with all other primary fields exact. Save/clock overrides are cleared, AUTO is off, normal portrait preview advances frames and the authored scene/font are preserved. The existing supported Editor-background override leaves an explicitly unstaged PlayerSettings mirror; it is not a changed packaged default or APK acceptance. No APK, new artwork/layout, audio or phone action occurred.
