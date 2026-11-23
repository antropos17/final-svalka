# Decision cadence and visible progress, seed1

Research completed against integrated production commit `dc012bf743899da2369d402cdf932ec2b37ecf94`. No balance, production C#, scene, save contract or UI recommendation changed. The preceding zero-outcome and active-intent slices were completed before this source freeze. The timed automation remains stopped.

The [source-bound packet](../evidence/unity/decision-cadence-20261102/verification.json), [economic ledger](../evidence/unity/decision-cadence-20261102/screen-first.json), [all72 construction intervals](../evidence/unity/decision-cadence-20261102/screen-first.csv), [opportunity intervals](../evidence/unity/decision-cadence-20261102/opportunities.csv) and [UI observation](../evidence/unity/decision-cadence-20261102/ui-observation.json) separate economic facts from automated presentation and uncollected human evidence.

## Source and bounded method

An exact full Git archive of the integrated commit was extracted under owned X: scratch. Only the research runner's `Program.cs` opt-in dispatch and new `DecisionCadence.cs` overlaid that frozen runner. `ProductionRoot` points at this immutable extraction. The existing csproj already compiles `*.cs`; it is unchanged. Historical default/candidate/payback modes were bypassed, not rewritten or invoked.

The first build passed with0warnings/0errors in6.83s. The actual MSBuild compile list contains69 explicit C# inputs plus2 generated files; their raw hashes, csproj identity and rule revisions are retained in [build-inputs.json](../evidence/unity/decision-cadence-20261102/build-inputs.json). Built assembly SHA256 is `3AB01655A7FA8BD874A9308FFC47B510631BEA4BFC7C7D9356BA620A0DCA92E1`. The compact source archive contains those exact inputs and project; build output identity is separate from Unity's observed loaded assembly. Source identities were compared before/after both stages. Git archive LF and some working-tree CRLF inputs have different raw hashes but identical code after only CRLF normalization; both input sets and the first failed raw-equality audit are retained, without rewriting source or rerunning gameplay.

Predeclared seed1 and policy order were fixed before execution. Every case starts through public GameSession/GameStore with37Coins, strict codec and in-memory private storage. Construction chooses cheapest current price then ordinal ID, retains the selected immutable price, enters the second era and explicitly acknowledges it. Skill priority uses only current public legal/affordable quotes. All policies identically defer resource/Arcade spending, mini-game start/claim and free presentation-speed changes; no postchapter farming, hidden/future draw selection or resource fork occurred.

Limits:800 foreground spins/case,2400 total foreground/auxiliary calls,90s process wall,2MiB economic JSON and512KiB CSV. The runner checks limits inside loops, persists the original shared budget, reserves bounded flush time and records censored/unexecuted cases. The one actual screen completed first, without timeout/retry:1730 spins plus one exact retained-spin replay,1731 calls total,61.141s external process wall /60.877s runner wall,1,141,745-byte JSON and4,773-byte construction CSV. The derived opportunity CSV also fits the CSV allowance. No case was censored or unexecuted. Build time is separate.

There were13,915 assertions and72,468 read-only quote reads. Independent wallet equations use gross income and actual debits, not net reward labels. Every construction is public owned-stage commit with its saved selected price/debit. Primary seed/symbol/base-payout identities agree at matched spin positions across the policies. Observers preserve complete encoded state and storage write count. The selected checkpoint roundtrips and an exact retained public spin request returns identical state with0IO; this is the one counted auxiliary call.

The literal [build command](../evidence/unity/decision-cadence-20261102/build-command.json) and [screen command](../evidence/unity/decision-cadence-20261102/run-command.json) are retained. Reproduction uses a new owned output/budget directory, not an overwrite or an additional authorized run:

```powershell
dotnet build <frozen>/Checks/ExpansionPacing/ExpansionPacing.csproj -p:ProductionRoot=<frozen> -p:BaseIntermediateOutputPath=<owned>/obj/ --output <owned>/build --nologo
dotnet <owned>/build/ExpansionPacing.dll --decision-cadence --source-root <frozen> --source-commit dc012bf743899da2369d402cdf932ec2b37ecf94 --input-manifest <owned>/build-inputs.json --output <owned>/out/first
```

Generate the manifest from the actual MSBuild Compile inputs, generated files and newly built assembly; retain the frozen source hashes. The supplied opt-in orchestration demonstrates this step. X: process-only TEMP/TMP, initialized .NET home and certificate-generation false were used; there was no dependency installation or certificate trust action.

## Economic results

| Declared policy | Spins to24 | Skills actually bought | Skill debit | Gross income | Construction debit | Final Coins | Largest construction gap |
|---|---:|---:|---:|---:|---:|---:|---:|
| Construction baseline, skills deferred |595|0|0|9295|9310|22|32|
| Cheapest affordable unlocked skills first |562|6|610|9907|9310|24|33|
| Baseline plus one legal U1 investment |573|1|120|9409|9310|16|32|

All rows satisfy `37 + gross income - construction debit - skill debit = final Coins`. All24 buildings are paid; no skill total is assigned to a policy that did not buy it. Final Parts/Potatoes/Energy are200/200/30, with unchanged caps200/200/30. Bonus remains locked, no active mini-game or Diamonds, and three retained Tickets. These held stocks are consequences of the common deferral policy, not evidence that optional spending lacks value.

| Construction | Baseline end/gap | Skills-first end/gap | One-U1 end/gap |
|---|---:|---:|---:|
| House I |1/1|1/1|1/1|
| Workshop I |19/18|19/18|19/18|
| Cart I |35/16|51/32|51/32|
| Recycler I |57/22|67/16|67/16|
| Power I |76/19|100/33|87/20|
| Pigeon Loft I |100/24|121/21|107/20|
| Workshop II |125/25|148/27|130/23|
| Cart II |153/28|168/20|156/26|
| Recycler II |177/24|190/22|179/23|
| Power II |204/27|215/25|204/25|
| Pigeon Loft II |229/25|236/21|227/23|
| House II |260/31|264/28|256/29|
| Yard Cart I |288/28|288/24|281/25|
| Yard Workshop I |312/24|309/21|304/23|
| Yard Recycler I |334/22|330/21|326/22|
| Yard House I |362/28|357/27|350/24|
| Yard Loft I |387/25|386/29|375/25|
| Yard Power I |411/24|407/21|398/23|
| Yard Cart II |442/31|433/26|427/29|
| Yard Workshop II |472/30|459/26|458/31|
| Yard Recycler II |502/30|483/24|485/27|
| Yard Loft II |532/30|509/26|515/30|
| Yard House II |563/31|535/26|543/28|
| Yard Power II |595/32|562/27|573/30|

These are deterministic cases, not population estimates, percentiles or retention thresholds. U1 is bought at spin33 for120Coins in both investing policies while Cart I remains selected: wallet125→5. Cart payment moves from baseline spin35 to51, while this one-U1 case finishes the chapter22spins earlier. Skills-first buys Recycling I at84 for160, Delivery I at126 for60, Recycling II at335 for90, Universal II at366 for160 and Delivery II at387 for20, in addition to U1. Its longest local gap is33 despite finishing33spins before baseline. Shorter total progression and longer individual gaps coexist.

The policies choose25/31/26 non-spin actions respectively, plus one era ACK each. Those actions include24 selections and one era entry; skill purchases explain the differences. They are policy commands, not counts of meaningful human decisions. Optional quotes continuing for many spins are one persistent opportunity. Tickets or resource credits alone are not choices.

## Opportunities inside the selected gap

The predeclared global maximum completed gap selects skills-first, Recycler I committed at67 through Power I committed at100. This is the selected extreme among all three results, not a typical interval. Its checkpoint comes from the complete public-command economic prefix, not edited ownership or hand-funded Coins.

Power I was legally selectable before affordability. At67, wallet5, selecting its190Coin quote retains an185Coin shortfall. Recycling I becomes unlocked after Recycler I at67, becomes affordable at84 with161Coins, and the policy chooses it immediately for160. The existing preview explicitly says future `+1 per Trash`, unchanged odds, remaining wallet1 and retained Power goal still needing189. After16 more spins, Power pays at100. In the one-U1 case with the same prefix through67 and no Recycling purchase, Power pays at87. This isolates a local investment/payment tradeoff; it does not prove either choice is preferable to a person.

Both mini-game start quotes are already legal from spin8 with earned Tickets and remain legal throughout67–100. Examples of continuing affordable public resource quotes include Harvest from7, Salvage/Packing/Dynamo from10, bonus unlock and Parts trade from15, Potato trade from19 and socket2 from24; Recycler equipment becomes legal at67. Free fast presentation is also legal. All remain deliberately unexercised here. Availability establishes neither useful marginal benefit nor notice, understanding or voluntary preference. Empty/equal loadout operations are not counted as consequential actions.

One measurement limitation is explicit: raw construction/skill observer onsets use progress-rule eligibility, including the zero-spin EnterEra→ACK boundary. Those states cannot be acted on until ACK. The derived opportunity table records the earliest phase-eligible sequence; do not interpret raw `construction-select`, `construction-pay` or skill labels during that boundary as actionable UI. Optional public quotes reject the pending era, yielding distinct legal intervals separated by that zero-spin phase. No blocked action was selected; the observed67–100 interval is unaffected. No repeat screen was needed to disclose this limitation.

## One actual private UI interval

The existing integration Editor6000.3.21f1 PID41580/port7800 was available after the previous slice completed. Pinned CLI beta.14 and its Pipeline drove the exact project. Research observer scripts remain outside `Unity/Assets`; no compiled production dependency, test fixture, recompile, Core49/EditMode or full UI campaign was introduced.

The economic start checkpoint is canonical SHA256 `99B1360A54C92BE8230863B48DA3E95270B72B1F74B1F4AE4DA92BED5FE42C1C`: sequence72, seed1685307013, wallet5, four buildings, U1 owned, resources102/52/30, no selected goal. The private clock is fixed at1700000268 to exclude accidental offline credit. This is an economic-prefix-produced checkpoint followed by an isolated UI interval; the prefix was not visually played or naturally chosen.

Routes used actual center-raycasted uGUI PointerDown/Up/Click against existing targets of at least48×48. AUTO stayed off, saved speed stayed normal3.5s, foreground and focus were observed throughout. The initial legitimate AUTO hint was closed through its button. That commits one acknowledgement: all35 economic actions match with sequence offset+1, not canonical snapshot equality. Recycling's displayed quote becomes `skillbuy.92` instead of economic-only `skillbuy.91`; the debit, ownership, benefit and selected quote match. Actual UI tutorial facts25 change guidance mask6→31. Fixed UTC and sequence-derived receipt keys differ as documented in [UI qualification](../evidence/unity/decision-cadence-20261102/ui-qualification.json).

| Observed event | Actual evidence |
|---|---|
| Input overhead |33 SPIN gestures;16 other gestures: tutorial Close1, selection route5, skill route5, read-only end inspection5. Each gesture dispatches3 pointer events:147 total, no AUTO start/restart. |
| Normal presentation |33 spins; Busyfalse first sampled3.538–3.956s after dispatch, sum117.727s. First subsequent uncovered root samples3.552–3.970s, sum118.198s. These include observer/Editor/save scheduling; they are not pure animation benchmarks. |
| Economic Power commit |Final SPIN immediately changes ownership4→5, gross income28 and saved debit190: wallet172+28−190=10. Normal reveal was still allowed to finish. |
| Root payoff |Observed at124.189s, Busyfalse/uncovered:5/24; `Energy: Power for the yard. +1 per Potato.`; `Paid 190 → Energy 1/2 · saved`; Next goal. No follow-up Spin. |
| Active building scene assets |HouseStage1 remains active and identical; Workshop/Recycler/Loft/Cart/Power drafts remain inactive. No visible building asset transformation was established for Power I. |
| Map and Progress |Only explicitly opened after interval stop: Map shows Power I Done and5/24; Progress shows5/24 and7 upgrades until Workshop Yard. This read-only inspection takes an additional1.028s and creates no economic action. |
| Observation limits |33/40 spins,125.217/300s including end inspection,147/200 dispatched events,7/10 captures,3,329,528/67,108,864 diagnostic bytes at qualification. Completed without mismatch/recovery/source drift/censoring. |

The33-spin animation-only nominal duration is115.5s. Nominal3.5/1.5s and AUTO+0.25s envelope fields in the economic output exclude menus, reading, era stops and explicit restarts; they are projections, not measured sessions. The observer adds declared0.12s settle intervals and frame waits; rapid menu captures may retain outgoing reward-flight overlays. It collected no human reading/hesitation/comprehension/enjoyment/boredom or voluntary continuation. One regular1080×2340 portrait was observed; no short-profile, OS-pointer or Android/device acceptance is claimed.

Selected captures: [retained goal](../evidence/unity/decision-cadence-20261102/ui-power-goal.png), [Recycling tradeoff](../evidence/unity/decision-cadence-20261102/ui-recycling-preview.png), [Power payoff](../evidence/unity/decision-cadence-20261102/ui-power-payoff.png), [Map](../evidence/unity/decision-cadence-20261102/ui-end-map.png), [Progress](../evidence/unity/decision-cadence-20261102/ui-end-progress.png).

## First failures, preservation and verification scope

There was one economic screen and one dispatched UI interval. Setup receipts preserve an unsupported `commands --project-path` argument, domain-reload blocking/one consequent Pipeline-unavailable profile request, an incorrect observer field name, an empty neutral scene with no game, and an unselected-price observer assumption. Each correction precedes interval dispatch; none is a failed gameplay case or reason to rerun the screen. Two local diagnostic readers needed explicit UTF8/quoting correction. Same Editor recovered after its normal domain reload; it was not replaced or dismissed.

Both latest normal jtn files remained byte/size/nanosecond-exact across private work. No normal SPIN or raw restore occurred. Normal preview was resumed with task-owned save/clock cleared; four existing capture overrides remain null. Resume407→409 changes only revision/sequence/lastSettledAt, retaining38Coins and the identical spin404 receipt. Normal frames1739→6821 advance, AUTOoff/Busyfalse/no recovery. Scene/font/preexisting settings and original Pipeline config remain exact. Regular official portrait, Scene/Hierarchy and locked serialized Canvas Inspector46486 are restored. The source-bound packet contains these point-in-time normal observations; those historical Coins/sequences are never restore targets.

Source/build scratch stayed below512MiB on X; UI diagnostics stayed below64MiB. Effective Editor temp remains the established X active-intent temp. Disposable closed scratch is eligible after seven days, without automatic deletion; receipts and user data are preserved. Disk and known diagnostic sizes are recorded without attributing unrelated free-space changes to this task. No global configuration, package installation, signing, APK, phone, publication, paid service or timed automation operation occurred.

## Two later localized hypotheses, not implemented

1. **Optional investment discoverability at Power67–100.** A restrained existing-Upgrades entry cue when Recycling I first becomes affordable at84 might help users inspect the genuine160Coin/retained-goal tradeoff. The current detail already explains it correctly. Expected effect: more informed inspection, not forced purchase or guaranteed faster construction. Tradeoff: header clutter and distraction from a nearly funded goal. Missing evidence: whether people notice the existing route and understand the investment/payment delay; this automated policy supplies no preference data.
2. **Power I's visible completion at100.** A localized acknowledgement on the existing Power/Map visual could make the paid milestone easier to associate with its yard effect while preserving the current narrative/receipt and avoiding another modal. Expected effect: recognition of one completed building; no assumed income or retention gain. Tradeoff: visual noise/art work when the root currently displays HouseStage1. Missing evidence: human recognition of the present5/24, text and map, plus a reviewed concrete visual. No balance retune is supported by this one seed.

No further implementation or campaign is authorized by this research. AUTO's explicit era/chapter stop/restart policy is preserved.
