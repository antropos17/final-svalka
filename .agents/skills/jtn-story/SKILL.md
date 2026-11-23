---
name: jtn-story
description: Design or revise Junk to Neon narrative beats, neighbor dialogue, chapter transitions and contextual guidance, tied to existing Unity gameplay and save state.
---

# Junk to Neon story

Use this for narrative and meaning; use `jtn-first-session` for pacing and `jtn-economy` for reward arithmetic. Follow [the project workflow](../../../CODING_STANDARDS.md).

Read the current narrative section of GAME_RULES, `Unity/Assets/Scripts/Guidance/ChapterNarrative.cs`, the affected guidance/runtime caller and only the relevant content definitions. Establish which event actually triggers the line, which conditions gate it and whether it is new, resumed or already acknowledged. Existing research under `research/narrative-v01` and `research/content-toolkit` is optional and may describe an older catalog.

Build a small arc from the player's visible goal: a concrete problem in the district, a player action, a changed place or neighbor response, and a reason to care about the next available goal. Let construction and earned effects carry the story. Keep normal reward acknowledgement brief enough to preserve the spin rhythm; reserve longer copy for explicit inspection. Use existing characters, buildings and eras before proposing new systems.

For each changed beat, identify:

- Existing trigger ID and prerequisites; source file/callsite.
- What the player sees before acting and what changes afterward.
- Final player-facing copy in the game's language; one or two concise lines where the portrait layout requires it.
- Repeat, reload, Back and acknowledgement behavior; actual next destination.

Differentiate an unlocked option, an accepted task, a completed task and a claimed reward. Dialogue must not promise an unavailable era, repeatable payout, unsaved progress or a benefit the Core receipt does not grant. Opening story text must not claim, spend, select a goal or advance a run. Keep story IDs and historical acknowledgements stable unless migration is explicitly part of the task.

If asked to implement, edit the owning content/guidance layer and wire the existing presentation. Use the relevant integration checks from `unity-verify`, including actual portrait text fitting when the display changes. If asked only to design, produce a compact beat table and state which lines remain proposals. Do not introduce Ink/Yarn or a new save schema for a few contextual lines; consider a dialogue engine only when branching authoring has become a concrete bottleneck.
