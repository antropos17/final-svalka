# Two-era narrative producer

`Unity/Assets/Scripts/Guidance/ChapterNarrative.cs` provides presentation-only Russian product copy for the two existing eras and all24 project IDs. `NarrativeBeat` is sealed with get-only `ProjectId`, `Title` and `Body`; the content array is private. Unknown/null completion IDs return false/null and unknown orders return null. Callers must select global text from existing committed facts; the helper does not decide when a chapter ends.

The interface agreed with the Runtime owner is `Opening`, `EraReady`, `EraEntry`, `ChapterEnd`, `TryCompletion(string, out NarrativeBeat)` and `OrderIntroduction(string)`. There is no Unity, Core, economic, clock, random, IO or save-state dependency. All existing source outside this new helper is unchanged. The meta GUID is `40acb54b06824558bf0061d0382143e7`.

The authorized contract is `docs/development/chapter-narrative-v02.md`. One false ordering assumption was removed with the contract owner's agreement: `house.improve` no longer says that other buildings are still waiting, because it can be the last Junkyard purchase. Neither house stage ends the whole chapter. Functional benefit and price text remain the Runtime owner's separate authoritative content.

## Focused verification

The first compile/run passed with exit0 in8.109 seconds. No retry or unchanged Core49/pricing312/Journey390 rerun occurred. The five groups establish:

- Independent literal24 IDs exactly cover the actual two-era catalog, with unknown/null fallback, immutable API and source text bounds (title48/body150 UTF-16 units). These bounds are not a Canvas fit claim.
- Two legal funded actual-Core paths buy all24 projects in different family orders: house-first/forward and house-improve-last/reversed. Both spend9310 and retain690 of private10000 funding, seed1 and zero skills. Every immediate completion is retrieved from the actual stored latest-completion identity and checked after strict-codec reload. Older completion acknowledgements refuse without writing; exact current acknowledgements replay without payment or another write.
- Both global boundaries require all relevant families. Unseen era entry survives reload; skipping its existing presentation only acknowledges it without changing coins/RNG. Existing final optional orders can be accepted/read/cancelled without progress, automatic reward or invented story state.
- A real fresh in-memory save grants37 once. One spin pays8 and completes40 repair, leaving5; one later AUTO spin retains the same completion. Reading/reloading does not replay it. A separate private current V3 skill purchase costs120 and exact replay does not charge again.
- Seven frozen snapshots emitted by genuine historical implementations preserve pending/paid construction600/540 and legacy V1 delivery-skill540. Nonpending opens write nothing; pending recovery writes exactly once. Pre-Journey saves gain no invented past completion. A separately labeled pinned V2 ownership module keeps180/5 and byte-identical round-trip; it is not presented as a historical full Core save.

There are138 full raw-storage/write-count/encoded-state comparisons across narrative reads and exactly two foreground spins. The two funded construction paths are private artificial fixtures, not pacing campaigns or player saves. The eight input hashes are in `evidence/result.json`; all48 compiled source/project hashes are in `evidence/source-freeze.json`, with zero post-run mismatches. Base source is `dcb8b1a`.

## Reproduction and preserved receipts

Run from an isolated source root; use X-based per-process TEMP/TMP, DOTNET_CLI_HOME, NUGET_PACKAGES and NUGET_HTTP_CACHE_PATH. Example from this producer checkout:

```powershell
dotnet run --project Checks/ChapterNarrative/ChapterNarrativeChecks.csproj -p:BaseIntermediateOutputPath=X:/tmp/jtn-gameplay-lifecycle05/.work/chapter-narrative/obj/ -p:BaseOutputPath=X:/tmp/jtn-gameplay-lifecycle05/.work/chapter-narrative/bin/ -- X:/tmp/jtn-gameplay-lifecycle05 <new-output-json>
```

An existing output is refused. The first process had an external60-second timeout; focused logic also has a60-second bound and a128KiB receipt cap. `first-run.stdout.log`, empty `first-run.stderr.log`, `first-run-exit.json` and before/after resources are retained under `evidence/`. New disposable scratch is1,197,391 bytes, below8MiB; evidence before this note was11,254 bytes. C free after20,809,490,432 bytes; RAM available13,630,776KiB. No Unity Library exists in this isolated copy and no Unity logs were created. Scratch retention: this one fixed directory is retained through review, with no repeated run or growing log loop; remove only after its receipts are accepted and the producer is no longer needed.

Helper SHA256: `230EDF921E6769114C1B4AABBD0157EA165930723F4D0576BF00551E793696F6`.

Result SHA256: `73020CE1F223D144B61A608D3098C7FFC1E25960F7AA59B896D079BB3CCBFC19`.

Source freeze SHA256: `7052F63812114C8E87C222D4240A71955FAC68C8B0BAE8CD963FDA61304D1651`.

Runtime integration, visible functional copy, actual label fitting, retained manual hint controls, warm-Editor compilation/EditMode and focused private UI acceptance belong to02. Independent combined review is required before primary integration. No APK, visual work, native/device or human-play acceptance is claimed here.
