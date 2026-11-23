# Oracle contract

The initial oracle was defined before the r9 campaign. This r11 update pins
`bd8fbe8e140627f0bbbd0763b174c8245457705d`; source-manifest.json records all45
linked sources. The original r9 manifest is retained in evidence/r9/source-manifest.json.
Sources are linked, never copied into a replacement game.

| Invariant | Independent expectation and source | Limit |
| --- | --- | --- |
| Wallet | Initial snapshot wallet + observed-symbol income - explicit accepted prices + explicit Claim reward. Literal V1 construction matrix plus independent V2 override house.improve540; literal current skill prices120/160,160/90,60/20 from GAME_RULES. No order reward before Claim. | Synthetic initial funding is separate from new operations; no offline-time oracle. |
| Spin | From three observed symbols: all equal23, exactly one pair8, different3. Pre-command workshop gives1; recycler gives trash count; pre-owned loft gives3 per completed delivery. Each owned skill contributes1 in its branch. Literal family-symbol mapping and stage thresholds encode the 19 content sources. | Observed outcomes do not independently prove RNG probabilities. All64 baseline outcomes are checked separately; exact seed advancement uses independent xorshift arithmetic. |
| Purchase | At most the explicitly selected target; literal price threshold; exactly one added completed project, no successor; skills append one receipt and preserve selection. | Availability discovery uses the real read-only catalog, so catalog availability completeness is not independently proved. |
| Construction revision | Explicit selection/reselection adopts literal construction.prices.v2 and540. Spin uses the selected context's retained revision: V1 house.improve costs600. Other commands cannot change that revision. V1/V2 totals9460/9400 are checked across24 prices. | Catalog Price600 is historical data, not the current offer. No physical recovery/pacing campaign. |
| Replay/refusal/query | Complete canonical strict-codec snapshot, storage bytes and write count unchanged for refused, replayed and observation operations. Successful mutations advance one sequence and write exactly two journal snapshots. | Canonical encoding is a production validation check, not an independent codec proof. |
| Ownership/era | Exact completed-project set, skill receipt identities and selected ID stay unchanged except explicit purchase or free explicit entry. Entry preserves wallet/skills/carry and advances six levels2->3; pending transition refuses foreground actions. | No UI presentation claims. |
| Delivery | `(oldCarry + observedPigeonsIfLoftPreviouslyOwned) / 3` deliveries and remainder; newly bought loft does not count acquisition symbols. Other operations leave complete carry checkpoint unchanged. | No timing/concurrency claim. |
| Orders | One explicit active instance; accept starts zero; only subsequent new spin counts trash appearances or completed deliveries, capped12/3; Claim120/180 clears instance; cancel clears without reward. Exact old claim preserves a newer instance. | Bounded retained identities; arbitrary aged requests need not replay forever. |
| Guidance | Completion sequence identifies the actual purchased project; old completion acknowledgement refuses while a newer completion remains; query/reopen cannot grant or mark tutorial. | Never supplies visible/preview presentation flags; suggestion priority is not an oracle. |
| Historical receipt | One V1 paid-slot fixture (wallet/house/context literal) decodes/encodes and remains at original120 price; additionally V1/V2 prices are checked against literal arrays. | Reuses qualified compatibility evidence; no second compatibility corpus. |
| Reopen | Strict encode/decode canonical equality; reopening committed memory bytes preserves entire state, writes zero and grants nothing. | Initial legacy projection may remain absent in bytes; query projection is allowed without materialization. No filesystem faults/native durability. |

Production exceptions at an explicitly expected refusal are valid only if the complete
snapshot and storage stay unchanged. Unexpected exceptions, classification mismatches
and invariant failures are **candidates requiring triage**, not automatic production bugs.
The first failure is retained before reduction. An unrelated reduction exception does
not match the original signature. Missing prerequisites in reduced traces are harness
dependency failures. Repeated production runs can establish determinism only.
