# Bounded Journey render-query probe

This executable compares the exact non-Busy `RenderJourney` query sequence at
`53276a25197131c463590eef6daecb673375947f` with a single live snapshot and
`JtnJourneyView.FromSnapshot`. It does not drive Unity or measure an entire render.

Seven inputs cover legacy/fresh origin, a saved V2/540 goal, 23 constructions/six
skills, era-ready guidance, completed chapter guidance and a ready unclaimed order.
Existing fixture bytes are loaded read-only. The store is private memory. Output
identities include the entire guide state, suggestion text, completion and transition;
state encoding and write counts must stay exact. Independent fresh-origin/null-hint
and transition assertions complement the before/after comparison. Mutating the
detached snapshot after projection must not change the retained immutable result.

Use PowerShell from the worktree root. All generated paths stay on X. Set the SDK
first-use variables before the build, including when choosing a new CLI home:

```powershell
$env:TEMP = "$PWD/.work/temp"
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = "$PWD/.work/dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
New-Item -ItemType Directory -Force $env:TEMP,'.work/probe' | Out-Null
dotnet build Checks/JourneyViewProbe/JourneyViewProbe.csproj -c Release --artifacts-path .work/probe/artifacts --nologo
dotnet .work/probe/artifacts/bin/JourneyViewProbe/release/JourneyViewProbe.dll . .work/probe/query-results.json
```

There are 16 warmups and eight paired 64-query blocks per variant/input, alternating
ABBA/BAAB order. Compare paired medians and allocation differences; raw allocation
figures include a common measurement-wrapper overhead. This is a .NET 10 Windows
microbenchmark, with no disk writes inside the measured query. JIT and timing noise
remain. Neither CPU medians nor allocation differences prove Editor/Android frame,
GC, persistence latency or native performance improvements.

Do not repeat this probe after unrelated content/UI changes. The parent may simply
reuse its immutable receipts or skip the optional hook. If integrated, preserve the
Busy early return and take the snapshot from `Session`, never the pre-spin display
state. Create the view only in the existing render, never per-frame or in a field.

Disposable closed build diagnostics under this worktree's exact `.work/probe` and
`.work/dotnet` directories have a 32 MiB combined stream budget and seven-day cleanup
eligibility. Retain source/fixture/execution receipts. No automatic cleanup is
installed; this finite probe's before/after sizes must be checked. Skip locked files
and reparse points in any later cleanup; do not clear unrelated caches or saves.
