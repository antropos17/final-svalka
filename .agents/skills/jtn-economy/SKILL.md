---
name: jtn-economy
description: Analyze or tune Junk to Neon prices, rewards, resource flows and upgrade value using current C# rules, player policies and reproducible progression calculations.
---

# Junk to Neon economy

Follow [the project workflow](../../../CODING_STANDARDS.md). A design/simulation request does not itself change production prices.

## Establish the actual model

Read the applicable current GAME_RULES sections and locate the owning `EconomyRules` partials, `ConstructionPricing`, catalog and session methods. Record source identity, rule revisions, initial state and relevant exclusions for numerical results. `ChapterData` alone is not the current quote table. Start with the README of the matching existing C# runner: `Checks/ProgressionTiming` for construction/skills, `Checks/ExpansionPacing` for resources, or `Checks/ArcadePacing` for tickets/mini-games/slot levels. They contain historical modes and different invocation contracts; ArcadePacing requires a pinned immutable `FrozenRoot`. Verify input hashes and covered mechanics before reusing results or running a mode. The old two-project Python analysis does not model the current game.

Map sources, sinks, caps and prerequisites for the affected currencies, including any coupled costs. Do not assign invented exchange rates. Distinguish gross income, credited income after caps and construction debits. Include purchase opportunity cost and the upgrade's remaining useful lifetime. An effect that exists is not necessarily worth buying. A local arithmetic/copy question needs only the relevant calculation; it does not require a simulation campaign.

## Compare player outcomes

Choose only policies relevant to the request: nearest affordable construction, intentional investment, no optional spending, manual versus AUTO, or resumed play. For progression redesign, include a plausible non-optimal choice to detect disproportionate penalties or dependence on one hidden build order. Keep explicit decisions explicit. Report the affected purchase/unlock times and longest no-decision interval.

Use exact enumeration or dynamic programming when tractable; bound runtime, spins/states and output size before execution. If sampling, declare seeds/sample count, report p10/p50/p90 with failures/censored runs, and compare candidates on the same seeds/policy. Keep unfinished runs visible and distinguish quantiles among finishers from completion probability. Separate assumed cycle time from observed input/animation time. Price divided by income is an estimate, not a first-passage distribution.

Prefer production C# functions for simulation. If an independent model is needed, compare representative spin outcomes, upgrade effects, caps, prerequisites and claim/reload cases against Core first. A JSON candidate or Python result is not Unity integration evidence. A small analysis should reuse existing checks rather than grow a second game implementation.

Present the baseline and at most two candidate changes, the affected values, expected player benefit, opportunity costs and evidence limits. Increasing every reward and price by the same factor changes number size, not purchasing power or time to the next action. Evaluate readable receipts and visible world change alongside amounts; do not claim a measured dopamine or retention effect from a simulation.

## When tuning is authorized

Change the current rule/price revision through the existing path, preserving selected old quotes, paid receipts and replay bytes. Cover the affected fresh/legacy, cap, overflow and claim boundaries. Use the relevant existing Core suite, including `Checks/ExpansionCore` or `Checks/ArcadeCore` for those subsystems; keep mandatory Core/Unity/EditMode gates through `unity-verify`. Add only the affected live journey. Separate compatibility/runtime evidence from simulated pacing and human preference.
