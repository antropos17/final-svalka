# Parcels focused checks

Run `dotnet run --project Checks/Parcels/ParcelsChecks.csproj -- <repository-root> [group]`.

Groups: `catalog1440`, `eligibility-cycle`, `moves-bounded`, `scores-claims-overflow`, `write-faults-recovery`, `compatibility-isolation`, `codec-tamper`, `history-graft`.

The runner links production source but supplies independently literal catalog predicates, score histograms and expected wallet deltas. It uses private memory storage and reads historical fixtures without modifying them. Freeze linked source before qualification; keep TEMP/TMP and generated build output in task-owned X: storage. It is not a pacing simulation or native durability check.

The companion `ParcelsUiTests` uses the existing private Unity fixture harness. MAIN alone executes its six cases and official portrait profiles. Optional `JTN_PARCELS_CAPTURE_DIRECTORY` must name a unique child of `.work/parcels-20261103/tests/captures`; maximum12PNG files, no overwrite. Those cases measure all four authored boards and capture the original round/confirmation/Ready flow plus preview at both sizes. Simulated uGUI pointers/Submit do not establish native touch acceptance.
