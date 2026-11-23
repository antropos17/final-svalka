---
name: jtn-first-session
description: Analyze or redesign Junk to Neon's opening progression and first ten minutes, including early wins and Monopoly GO-like momentum. Use UI skills for isolated visual fixes.
---

# Junk to Neon first session

Follow [the project workflow](../../../CODING_STANDARDS.md). Use `jtn-economy` only when numerical pacing needs calculation and `jtn-story` when the goal lacks narrative meaning.

Start with a specific player problem: unclear next action, an unseen payoff, too many panels, an upgrade whose effect is missed, or a long wait without decisions. Read the current first-action, navigation and affected reward contracts, then trace those through Runtime/Core. Treat old first-ten-minute reports as historical hypotheses until their model matches today's source.

Map the affected opening segment as **action -> confirmed reward -> visible change -> understandable next goal**. Record input, precondition, actual credit, visible payoff, next choice and interruption cost. For a whole-opening comparison include manual/AUTO, skipped optional spending and a plausible non-optimal purchase; examine pause/return when that path matters. A single beat/copy request stays local. Use private fresh state for live observation and preserve ordinary progress.

## Candidate rhythm, not claimed benchmarks

For a requested redesign, the following are starting hypotheses to measure, not facts about MONOPOLY GO or mandatory new mechanics:

| Window | Desired experience | Observe |
|---|---|---|
| First 30 seconds | Understand the goal, perform the main action and see one real success | Time/taps to first action; whether the player can explain the result |
| 30-120 seconds | See a world change and choose an understandable next goal | Time to repair/purchase; recognition of its benefit |
| 2-5 minutes | Feel an existing upgrade in later play and make another useful choice | Decision gaps; credited before/after effect; unnecessary panel changes |
| 5-10 minutes | See medium-term progress and a natural stopping/resume point | Remaining goals, clarity of saved progress, voluntary continuation |

Move these windows when current rules or observations justify it. Before adding rewards, expose the benefits and optional routes already present. A ticket every eight eligible spins is not automatically a Diamond payout; the player must start, complete and claim the mini-game. Opening a card is not consent to spend or claim.

For large numbers and frequent wins, distinguish legibility, purchasing power and variety. Show actual committed credits once; separate included bonuses from additional grants. Use brief anticipation, an accurate receipt and an obvious change to the district. Preserve an accessible reduced-effects path. Do not fabricate wins, hide costs, imply an unearned jackpot or introduce pressure timers to compensate for unclear gameplay.

Deliver one concrete sequence using existing mechanics, with the bottleneck it addresses and an observable success criterion. Suggest at most two variants when comparison is useful. Small observation sessions can find confusion; they do not establish D1/D7 retention or a causal engagement uplift. Report counts and limits, not invented percentages. Implement only when requested, then verify the affected live sequence through `unity-verify` without replaying unrelated acceptance campaigns.
