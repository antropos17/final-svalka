# Independent batch exchange checks

The runner links production Core source and asserts independently literal economic outcomes. Run `dotnet run --project Checks/BatchExchange/BatchExchangeChecks.csproj -- <repository-root> [group]`. Groups: `boundaries`, `invalid-stale-overflow`, `atomic-recovery-replay`, `codec-isolation`.

For reproducibility, freeze the source directories named in the project and this runner before execution. Keep TEMP/TMP and build artifacts under private X: task storage. The runner uses memory storage and reads historical fixtures; it never writes ordinary saves or historical exports.

Initial expected cases were specified before execution: both resources19/20/39/40/95; legacy quantity1; quantity2;95Parts/7Coins quantity3=>35Parts/67Coins; Max4=>15Parts; upgraded299/281 stock max14; Coin checked boundary; invalid/stale quote0IO; two journal writes without public converted balances during either callback; before/after landing failures at both writes; recovery/replay; v4 dispatch with modern Arcade state; canonical historical fixtures; rehashed malformed payloads; exact unrelated lower/expansion data including RNG/ACK/selected540/600.

`BatchExchangeUiTests` is a separate PlayMode class reusing the existing private save harness. MAIN alone executes Unity tests and portrait captures. Pointer checks are simulated uGUI input, not native touch. Three cases cover preview/Back/quantity3/result/single repeat, Max/held/queued quantity/pause, and insufficient/session replacement/current staleness. Regular and short capture acceptance remains with MAIN.
