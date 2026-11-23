# Functional guidance handoff

Date: 2026-10-29 (Asia/Baku). Stream 06, source baseline `042fb52` (r8),
branch `codex/guidance-wave09`, isolated source-only worktree
`X:/tmp/jtn-effects-wave06`. Prior Effects branches and commits are preserved.

## Scope and authority

Original `PLAN HERE.html` task 29 requests short resumable onboarding through
preview, explicit Start Project, a manual spin, visible transformation and AUTO.
The initial 37 coins are already granted by fresh-save creation; guidance must
never grant them again, select a project, manipulate outcomes or bypass normal
gameplay. The final owning boundary is
`docs/development/journey-pricing-contract.md`, released as `2764c29` by 02.

This is a NEW pure `JunkToNeon.Guidance` module with a Contracts-only dependency,
new tests and this handoff. It does not edit Effects, Core, codecs, runtime,
scenes, Content, Upgrades or settings and does not activate itself. No Unity
Editor, APK build, live save, clock, wallet, RNG, filesystem or server participates
in the policy. The strict module codec is an in-memory nested payload for the
existing root snapshot, not a second save file.

## Required integration behavior

- Core supplies explicit Fresh, Existing or LegacyExpanded origin. A missing
  preexisting r8 guidance aggregate is Existing. LegacyExpanded requires an actual
  pre-chapter r7-complete snapshot. Never classify by wallet 37 or a guessed
  sequence. Only fresh save creation uses Fresh; existing players skip first-use
  steps. Querying projected defaults does not write or grant anything.
- Suggestions are inline, short and nonblocking, with stable localization IDs.
  They never cover controls, pause AUTO or require a tap for each purchase.
  Existing mandatory transition/return panels retain their integration ordering.
  Domain English strings are supplied; Russian UI phrasing belongs to 02.
- A relevant unacknowledged `guide.expanded-r7` notice has highest guidance
  priority and honestly explains that saved progress remains while the chapter
  has expanded. A reload before successful ACK repeats it; a committed ACK does
  not resurrect. Modern chapter completion alone must not fabricate that origin.
- The last actual construction completion remains separate from the current
  suggestion and stays visible through later AUTO spins or acknowledgements.
  No UI action may create a second economic completion event or reward.
- Only the four approved new foreground operation kinds update event metadata:
  spin, project selection/purchase, skill purchase and explicit era entry.
  Preview, offline settlement, clock changes and transitions do not manufacture
  spin observations. Guidance ACK is a distinct metadata-only global-journal
  command and must never fall through to generic affordable-target purchasing.
- Chapter end is factual: all six families must reach Workshop Yard absolute 5.
  Optional remaining skills/orders are reported honestly, without promising a
  nonexistent next era or a required spending sink. Skills/orders do not gate
  the already completed chapter. Orders are repeatable instances of the same
  two types; claiming one does not permanently exhaust that optional activity.
- Core validates/freeze/persists guidance before/after metadata in its sole
  reserve/write/apply/write queue. Failed ACK writes leave the current committed
  state intact and use existing reload/recovery. No second queue or global
  configuration change is introduced.

## Published API for 02

The module owns its guidance-specific immutable values; shared stages and stable
project identities come from the exact existing `JunkToNeon.Contracts` assembly.
No duplicate stage/content/effect DTO is introduced.

```csharp
enum GuideOrigin { Fresh, Existing, LegacyExpanded }
enum GuideOperationKind { ForegroundSpin, ProjectSelection, SkillPurchase, EraEntry }

new GuideContext(FamilyStages stages, string selectedProjectId, int skillCount,
    bool legacyExpanded, bool ordersAvailable, bool orderActive, bool orderReady);
new GuideObservation(long sequence, GuideOperationKind kind,
    GuideContext beforeContext, GuideContext afterContext, string actionId,
    string completedProjectId, bool manualSpin = false, int observedTutorialBits = 0);
new ProjectCompletion(string projectId, long sequence);
new GuideState(GuideOrigin origin, int acknowledgementBits, int tutorialBits,
    long lastObservedSequence, string lastObservationKey,
    ProjectCompletion lastCompletion, long lastAckSequence, string lastAckHintId,
    long completionAcknowledgedSequence = 0);

GuideState.Create(origin);
GuideFlow.Observe(GuideState before, GuideObservation observation); // GuideProposal
GuideFlow.Acknowledge(GuideState before, GuideContext context, string hintId,
    long sequence, long expectedCompletionSequence = 0); // GuideProposal
GuideFlow.Query(GuideState state, GuideContext context); // GuideEvaluation
GuideStateCodec.Encode(state);
GuideStateCodec.Decode(payload);
GuideStateCodec.DecodeOrDefault(payload, explicitOrigin);
```

`GuideEvaluation` reports a nullable current `Suggestion` and an independent
`LastCompletion`. `GuideProposal` contains immutable `Before`, `After`, `Sequence`,
`Changed` and pure `Apply(current)`. Its result is metadata only, with no wallet,
reward, selection or RNG delta.

The eight hint IDs are `guide.welcome`, `guide.choose-project`,
`guide.first-repair`, `guide.skills`, `guide.era-ready`, `guide.expanded-r7`,
`guide.chapter-end` and `guide.orders`. The special ACK ID `guide.completion`
requires the exact expected completion sequence; it acknowledges that record
without deleting the stable last-completion information. A late dismissal of an
older completion must never dismiss a newer one.

Fresh tutorial flags are bounded to five known bits: Previewed 1, Selected 2,
ManualSpin 4, VisibleImprovement 8, AutoInformed 16. They describe factual
observations supplied by the caller, not permission to manufacture interactions.
One operation observation contains all of its facts, so selection plus an
immediately affordable completion is processed together before deduplication.

`ProjectCompletion.EffectInfoKey` is a stable derived presentation lookup key,
not an effect executor or a promised numeric income increase. The UI must use the
actual registered project's description; `house.improve`, for example, is a
milestone and must not be described as an invented income bonus.

## Small integration hook plan

1. Project or create guidance from explicit Core provenance. Persist it only with
   an actual existing-journal command, retaining strict absent-versus-present
   payload handling. Bind the context's factual legacy marker to the persisted
   origin; do not infer it from coins, current stage appearance or modern final
   completion.
2. For one new foreground spin, explicit project selection, skill purchase or era
   entry, build one complete `GuideObservation` from that operation's pre/post
   shared stages and factual signals. Freeze and apply its metadata proposal in
   the SAME pending/applied economic snapshot. Selection plus immediate purchase
   carries both facts in that one observation. Do not add a separate observe
   command after a spin, and do not replay old historical operations into new
   onboarding metadata.
3. Render `Query` through existing inline label slots. The suggestion is short
   and dismissible; the independent last-completion/project-effect description
   remains available even when AUTO updates its normal spin status. Do not treat
   this policy as authority to stop AUTO, unlock it, select a project or grant
   income. Core/UI must supply only interactions actually observed; metadata is
   not proof that a transformation was rendered on a physical device.
4. On hint dismissal, route the stable ID through the explicit guidance ACK
   journal command with the next global sequence. Completion dismissal also
   passes the sequence that was displayed. Freeze the proposal before writing;
   an old completion's late ACK must not acknowledge a newer record. The ACK
   handler does not run construction purchase post-processing.
5. Keep transition and offline-return panels ahead of ordinary inline guidance,
   preserve the existing recovery block after uncertain writes, and qualify the
   connected flow with private saves in 02. No new save file, timing loop,
   tutorial overlay or scene regeneration is needed by this module.

## Bounded persistence and replay

`GuideState.Version` is 1. State holds exactly the published finite fields, without
an unbounded event/ACK list. The strict canonical payload has 11 `|`-separated
tokens: `g1`, origin, eight hint ACK bits, five tutorial bits, last observed global
sequence, 64-character observation digest, last completed project or `-`, its
sequence, last ACK sequence, last ACK ID or `-`, and the acknowledged completion
sequence. Registered-shape IDs used here cannot contain the separator. Encoding
and decoding use invariant nonnegative integer text; unknown versions, missing
or extra fields, malformed IDs/flags/digests, noncanonical numbers and invalid
sequence relationships reject. The maximum payload is 4,096 characters.

`Decode(null)` rejects. `DecodeOrDefault(null, explicitOrigin)` is only for a
root field that Core has already proved absent. It must not turn a PRESENT null
or corrupt state into a fresh/default guide. Persist the payload as a bounded
nested string in the root snapshot, never a separate file.

An observation binds the exact typed operation, pre/post contexts, action,
completion and factual tutorial signals into a deterministic SHA-256 identity.
The same observed sequence with a different identity rejects. Older validated
observations are ignored without rewriting the latest completion, ACK or cursor;
their full historical identities are not retained or authenticated. Reusing an
ACK's sequence for a different event, or applying a stale new event between the
observed and ACK cursor, rejects. A proposal applies only against its exact frozen
before state or its already-applied after state.

Completion ACK stores an identity, not deletion. New completion B after an ACK of
A keeps global history, replaces the last completion and resets only the new
record's ACK marker. A late ACK targeting A rejects and cannot hide B. Later AUTO
observations, skill purchases and other suggestions preserve the last completion.

## Verification and storage

The standalone module harness passed **10 behavioral regression groups**:

- Explicit fresh provenance and already-granted starting-coins wording, without
  any reward, wallet or RNG API.
- All five fresh tutorial facts survive each codec roundtrip; initial preview,
  explicit selection, manual spin, improved stage and AUTO information remain
  distinct. Existing players skip those first-use suggestions.
  A committed repair alone does not set VisibleImprovement: after a possible
  pre-presentation reload only the completion receipt exists. Confirmed UI
  presentation is a separate factual bit carried into the next legitimate
  operation, without inventing another completion or journal event.
- Factual legacy-expanded origin is enforced; unacknowledged notice resumes,
  acknowledged notice stays dismissed, and its text remains honest if the player
  completes the expanded chapter before dismissing it.
- Completion A at sequence 2, ACK at 3, B at 4, stale ACK of A rejected, ACK of B at
  5 accepted. Completion/effect lookup remains independent through AUTO, ordinary
  hint ACK and skill purchase.
- Exact/latest and older observation replay, conflicting same-sequence events,
  ACK/observation high-water relationships and retry/stale-proposal behavior.
- Validated era entry adds no completion; chapter-end wording respects optional
  repeatable orders, actual availability and remaining skills, with no next-era
  promise. Invalid stages/targets/manual/AUTO facts and fabricated completions
  reject.
- Strict bounded codec failures, immutable children and nonblocking deterministic
  queries. Codec tests are in-memory and do not exercise production save files.

```powershell
$env:TEMP = "$PWD/.work/guidance-checks/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet-home"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project Checks/Guidance/GuidanceChecks.csproj -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true
```

The final stdout receipt is `.work/guidance-checks/final-review-fixed.txt`
(1,232 bytes), with 10 passing groups. There were no build or assertion failures.
The earlier 1,233-byte `.work/guidance-checks/final.txt` verdict is preserved and
is not relabeled as testing the final review fix. A text correction first kept an
ignored expansion notice honest after chapter completion. The independent
read-only reviewer then found one P2 semantic issue: completion was automatically
marking visible presentation before the UI had necessarily rendered. That
inference was removed. The regression now independently expects tutorial bits 7
after commit/reload, 23 after AUTO-information ACK (still no visibility), and 31
only after an explicit actual-visibility signal in a later legitimate operation.
The last completion remains sequence 4 throughout. The scoped suite passed after
this code/test correction; the reviewer read the fix and confirmed the P2 resolved
without rerunning tests. No other blocking issue remained and no unrelated suite
was repeated. A Unity test wrapper is supplied but was not run here.

Initial free space: C 10,643,312,640 / X 95,510,167,552 bytes. Final worker sample:
C 10,646,466,560 / X 95,498,702,848 bytes after the review fix. Guidance-only
disposable build/test artifacts measured 29 files / 561,771 bytes on X:.
Concurrent streams use both
drives, so samples do not attribute every drive change to this task. The task's
obj/bin/temp and private CLI home stay under X: `.work`; certificate generation
was disabled before every invocation. No Editor/import/Library was used.

Retention: preserve the source commit/handoff; the exact
`.work/guidance-checks/temp` disposable diagnostic folder has a seven-day / 8 MiB
budget before reuse. Inspect before another batch and remove only confirmed
closed disposable regular files there, skipping locks and reparse points. No
system Temp, ordinary save or other stream is cleaned; no automatic rotation is
claimed.

Remaining acceptance belongs to 02: strict root-field wiring and frozen
before/after metadata, factual event sourcing, all uncertain-write positions,
Russian inline rendering through actual UI slots, AUTO completion persistence,
ordinary-save protection, Unity compilation/EditMode/UI and any combined APK.
This module result is not a claim that onboarding is already connected in the UI.
