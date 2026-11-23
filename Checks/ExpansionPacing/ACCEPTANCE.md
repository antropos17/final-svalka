# Pacing comparison targets (declared before measurement)

Baseline source: `b2b2ae6`. Compare three fixed seeds (1,12305,305419896) under
three distinct deterministic policies. This is a bounded scenario comparison, not
a broad Monte Carlo sample or evidence of player enjoyment. Do not change existing
construction, frozen receipt, skill or base payout terms to make targets pass.

1. **Early useful choice:** a newly earned-resource shop item with a real ongoing
   effect can be explicitly purchased in5..15 future spins in at least two of the
   three fixed seeds, with no fixed-seed case above20. Report first affordability
   separately from actual policy purchase. Cosmetic themes do not satisfy this.
2. **Further useful choices:** a policy that pursues expansion can acquire at least
   three distinct functional shop/equipment choices within50 future spins. Report
   unlocked/affordable-but-deferred items and currency shortfalls at10/25/50/100.
   These are testable targets for candidate catalog tuning, not baseline promises.
3. **Construction preservation:** matched policy/seed retains24 paid constructions,
   original9310 current-offer total, unchanged spin RNG stream/base payouts and610
   total current optional-skill cost. Resource-only shopping should not delay a
   matched construction trajectory. Coin exchanges may accelerate it, which must
   be reported explicitly; never silently count the exchange as base spin income.
4. **Energy:** free spins always remain possible at zero Energy. The first useful
   booster should be affordable in5..15 future spins. Report actual explicit arm/
   consumption interval, cap, waste at cap and chosen policy, including a hoarder
   who never arms. No wall-clock refill or background resource accrual is assumed.
5. **Potatoes and specialization:** at least one potato-consuming useful action is
   reachable by25 future spins; potato-yield and salvage-yield policies produce a
   concrete difference in holdings, purchases or exchange outcomes. Report the
   opportunity cost and bottleneck rather than declaring one policy best.

Measure milestone spin counts, largest interval, first useful optional acquisition,
resource balances/spends/shortfalls, cap/boost cadence and shop actions. A missed
target remains visible. Recommend at most one narrowly justified NEW catalog price/
effect adjustment packet after the first frozen candidate; compare changed tradeoffs
once rather than scanning values until a favorable result appears.

Bounds:3 seeds×3 policies; at most800 spins/run,6,000 total spins,180s total wall
time,2MiB JSON and512KiB CSV; private in-memory GameStore/strict JsonStateCodec.
Stop and retain first failure/partial result. Fixed virtual foreground timing is
3.5s first-repair/manual,3.75s after repair; no decision/reaction or human pacing
claim. No UI journey, Unity, ordinary saves, network, profiler or native execution.
