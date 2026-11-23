# One-profile Arcade pacing utility

Read `policies.md` for the independently frozen outcomes, action policies and bounds.
This new project does not edit or run earlier ExpansionPacing modes. Compile only
the actual archived Core fa9f3f20df6fe71de64e6045ad7e09cb97d0b3cc. All purchase/play
actions use the public displayed-sequence quote API; masked current items/revealed
faces alone determine game choices. Internal challenge seed is measured only to
assert isolation, never read as a decision oracle or to select a favorable board.

The two earned policies are deliberately scripted low-income Sorting3/8→1Diamond
and visible perfect8/8→3. Neither predicts human difficulty. Ticket holding/Memory
uses only prior visible reveals. Paid synthetic level pairs equalize resource
headroom, compare actual TOTAL credits and retain exact Diamond/resource costs.
Included slot components are never added to those totals again. No full-currency
ROI is inferred from a40-spin resource component screen.

PowerShell from this worktree; export the frozen tree with `git archive` into a new
`.work/arcade-pacing/frozen-fa9f3f2` folder first. Preserve first receipts:

```powershell
$env:TEMP = "$PWD/.work/arcade-pacing/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet build Checks/ArcadePacing/ArcadePacing.csproj -c Release -p:FrozenRoot="$PWD/.work/arcade-pacing/frozen-fa9f3f2" --artifacts-path .work/arcade-pacing/artifacts --nologo
dotnet .work/arcade-pacing/artifacts/bin/ArcadePacing/release/ArcadePacing.dll --source-root .work/arcade-pacing/frozen-fa9f3f2 --source-commit fa9f3f20df6fe71de64e6045ad7e09cb97d0b3cc --output .work/arcade-pacing/first
```

One execution only. A missed threshold or unobserved first post-purchase effect is
reported as such, not replaced by another profile/seed/run. First exceptions retain
partial rows and the actual active private snapshot. Full producer score/fault/
historical matrices are reused explicitly instead of another duplicate suite.
Normal3.5s/fast1.5s wait estimates may be derived from the same counts only; runtime
cost, game inputs and human pacing remain separate. UI/Editor/physical durability/
native/FPS/enjoyment acceptance is outside this utility.

Disposable diagnostics stay under this exact `.work/arcade-pacing`, maximum32MiB,
with seven-day eligibility only for closed disposable files. Preserve immutable
source/input/failure/execution receipts and skip locked files/reparse points. No
automatic rotation/cleanup is installed. Existing initialized X .NET home and older
receipt trees remain preserved; check actual disk/output growth around the batch.
