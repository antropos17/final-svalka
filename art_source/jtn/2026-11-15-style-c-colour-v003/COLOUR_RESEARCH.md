# Colour research applied to Style C v003

Checked 15 November 2026. The task is to revise existing game art after the user found v002 too dull: more pleasant colour, a little crisp red and modest additional detail. This is an art-direction decision, not a claim that a particular palette raises dopamine or improves retention.

## Evidence used

1. [Wilms & Oberfeld, Color and emotion: effects of hue, saturation, and brightness](https://link.springer.com/article/10.1007/s00426-017-0880-8), published online 13 June 2017, journal volume 2018. In a controlled study with 62 participants, brightness, saturation and hue affected reported valence/arousal; bright saturated colours also elicited stronger skin-conductance responses. Hue interacted with the other dimensions. Dopamine was not measured. These colour-patch results do not establish a universal mobile-game palette.
2. [Jonauskaite & Mohr, Do we feel colours? A systematic review of 128 years of psychological research linking colours and emotions](https://link.springer.com/article/10.3758/s13423-024-02615-z), 13 January 2025. The review covered 132 articles and 42,266 participants across 64 countries. Many-to-many associations depended on hue, lightness and saturation; yellow/orange were associated with positive high-arousal emotions, while red had both positive and negative high-arousal associations. Most evidence addressed abstract associations; the authors caution that experienced emotion and specific contexts remain separate questions.
3. [Riot Games, Ask Riot: Let's Talk Clarity](https://www.leagueoflegends.com/en-us/news/dev/ask-riot-let-s-talk-clarity/), 9 April 2021. The art director describes colour relationships together with proportions, material and silhouette as part of maintaining recognizable identities across recolours. Applied as a craft reference for the same house/symbol identities, not as evidence about this game's players.
4. [Riot Games, VALORANT Shaders and Gameplay Clarity](https://www.riotgames.com/en/news/valorant-shaders-and-gameplay-clarity), 30 June 2020. The technical account describes deliberate highlight/midtone/shadow control and material simplification for readability. Only those visual principles transfer here; no shader or rendering technology change is proposed.

## Concrete design decisions

The following choices are design inferences from the research and the user's feedback, not scientifically prescribed ratios or guaranteed emotional outcomes:

- Replace muted teal/olive and brown-dominant midtones with clear turquoise/aqua, luminous vanilla cream, honey gold, fresh leaf green and lavender-blue shadows.
- Use clean vermilion `#EF3E36` in bounded areas: house chimney/door and final-state pots; tiny potato cheeks and box tape tab; trash tie and pigeon feet; a few UI glyph accents. Red does not become an all-over wash.
- Keep a common deep blue-teal contour and fixed camera/silhouette so stronger colour does not change object identity. Simple cool shadows separate warm materials without a muddy grey overlay.
- Add purposeful detail clusters: brick mortar, paired metal fasteners, window panes, a few broad timber creases and feather/bag folds. Preserve quiet large shapes and phone-size readability.
- Keep primary action skins gold, secondary skins turquoise, neutral skins cream and disabled variants visibly quieter. Labels remain separate. Control states use shape/press treatment as well as colour.
- Show v002 and v003 on the same navy background and at the same scale. A brighter preview background must not substitute for changing the assets themselves.

## Skill application and scope

`imagegen` governed local-reference edits, transparent output, original/prompt preservation and versioned project saving. `frontend-design` contributed subject-grounded palette decisions, a coherent visual identity and restrained accents; no web layout, typography replacement or application code was introduced. The Product Design ideation workflow and Emil animation material were inspected for relevance but were not applied: the request revises a selected raster game-asset set, rather than requesting alternate product flows or motion.

Acceptance is visual review of the new set, true alpha, complete separated exports and preserved stage identity. No dopamine measurement, engagement experiment, Unity import or device acceptance is claimed.
