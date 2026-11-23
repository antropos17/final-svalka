# Junk to Neon: art direction and asset production plan

Research date: **14 November 2026, Asia/Baku**. Research baseline: **29f1dc3d4cb590b501426468ce1a04248e80d94e**, plus the existing working tree. Status: **proposal for review; no direction approved for production yet**.

**Skills recheck, same date:** reviewed the newly installed Emil Kowalski package against current source at **7f2c2376945006e90a175767f555508097dd51a2**. Direction A and the 30-state inventory stand. Revised the motion recommendations to remove routine navigation transitions, require immediate visual press response without changing release consent, and verify interruption/reduced presentation in the first slice. [Section 16](#16-recheck-against-the-new-emil-skills) records findings, newer captures and source drift. This remains research/planning only.

## 1. Recommendation and boundaries

Choose **A: The Neighbourhood Workshop**: a compact painted 2D courtyard with shallow illustrated volume, warm repaired materials, a recognisable pigeon, and small cyan signs and lights. Keep uGUI/TMP responsible for text, quantities, prices, navigation and states. Build one visual slice before producing the complete collection: the existing root screen, House states 0–4, and the four ordinary symbols.

The principal problem is the seam between visual systems: pixel-art finds, geometric yard placeholders and generic catalogue glyphs describe different worlds. Another round of rounded panels alone will not close that seam. The recent hierarchy and wording improvements are valuable and should be retained.

This research authorizes this document and its STATUS handoff. It does not authorize image generation, asset acquisition/import, code or scene changes, gameplay changes, packages, subscriptions, API calls that generate content, APK packaging or publication. MAIN retains the only live Editor. This chat performed no Unity/CLI/Play/save operations and launched no agents. The existing deleted house texture and historical images were not restored. Source, normal progress and unrelated work remain outside this research's edits.

**Observable result for this stage:** a source-bound inventory, an actionable recommended visual language, an evidence register, production templates and small implementation waves that another agent can execute after a separate production request.

## 2. Current visual diagnosis

These are observations of existing PNGs, not a new running-game or Android assessment. Coordinates below are approximate fractions of each full image, measured from the upper-left. Values visible in private fixture images are not the user's normal earned balances.

| Frame inspected | Concrete observation | Production implication |
| --- | --- | --- |
| [Root, 1080×2340](../evidence/unity/visual-sol7-20261114/root-regular.png), yard around x .18–.80 / y .32–.50 | Three detailed pixel symbols sit below a yard made from sparse flat components. The House ruin reads more like a large broken icon than an inhabited place. Surrounding sites are small and low contrast. | Unify illustration treatment; give each family an identifiable mass and footprint. Design in the actual yard allocation before painting details. |
| [Root, 1080×2280](../evidence/unity/visual-sol7-20261114/root-short.png) | Large reels and a strong amber SPIN provide a clear action area. Map/Event/Bonus controls share the compressed world region. | Preserve action priority and current route clearances. Extra atmosphere must fit behind the six inspection areas. A larger illustration must not cover the hint or buttons. |
| [Modules](../evidence/unity/visual-sol7-20261114/modules-short.png), middle grid | Salvage, Packing, Harvest, Combo and Dynamo share the same tiny box glyph. Text carries almost all item identity. | Give the five existing modules distinct silhouettes; use one card component and real labels. Keep availability/ownership as explicit states. |
| [Equipment](../evidence/unity/visual-sol7-20261114/equipment-short.png) | Six family names have the same small House glyph. The current grid is orderly, but the collection has little visual personality. | Reuse family thumbnails derived from approved building art. Show level with a code badge, not twelve unrelated illustrations. |
| [Bonus, earlier short frame](../evidence/unity/visual-sol7-20261114/bonus-short.png) and [newer Workbench I](../evidence/unity/visual-sol8-20261114/workbench1-short.png) | The newer title grouping is better. Bonus Reel, Workbench and focus choices remain text-led, with repeated bolt/upgrade glyphs. | Preserve the newer grouping. A workbench illustration and differentiated focus symbols can add meaning without changing prices, ownership or selection rules. |
| [Settings, Sol8 normal short](../evidence/unity/visual-sol8-20261114/settings-normal-short.png) | Consistent margins, cream panel and readable grouped captions already form a useful UI base. Large uniform navy tiles still resemble a prototype control catalogue. | Retain layout and semantic grouping. Add restrained material cues to headers/frames; do not fill utility screens with scenery. |
| [Memory preview](../evidence/unity/visual-sol5-20261114/memory-short.png) | A long explanation is legible but occupies most of the play surface. This frame is the no-Ticket preview, not active Memory. | A compact diagram could supplement the existing explanation later. Do not remove reward/consent information or infer active-game readability from this frame. |
| [Parcels partial assignment](../evidence/unity/visual-sol6-20261114/parcels-partial.png) | A/B grouping, stable token numbers, names and location text explain the puzzle. Small pixel images provide flavour but little emphasis. | Reuse the same four approved symbols at puzzle scale. Keep token identity, empty/selected/placed states and A/B distinction readable without colour. |

At the initial research start, the latest complete STATUS entry was **“Sol High visual pass: Settings clause and Workbench title grouping (2026-11-14)”**, lines 3–13. It links [visual-sol8 verification](../evidence/unity/visual-sol8-20261114/verification.json): previous compilation, EditMode and private UI acceptance. These are historical receipts; this research neither reran them nor converts them into art or native acceptance. Sol7 remains useful for Modules/Equipment; Sol8 supersedes its Settings/Workbench wording views. The later skills recheck uses the newer root, Orders, Settings and building-detail evidence in section 16; do not treat the initial frames as the latest complete product snapshot.

### Scoped craft score

Interpretive scores for the inspected stills, not automated measurements or a release rating:

- palette discipline: **3/5** — cream/navy/amber/cyan are recognisable, but the illustrated world is not yet developed;
- style uniformity: **2/5** — pixel finds, line glyphs and flat yard geometry meet visibly;
- lighting/depth treatment: **2/5** — symbol shading and ground shadows use different visual vocabularies;
- silhouette: **2/5** — four finds are distinct; small construction sites depend heavily on position and labels;
- UI intentionality: **3/5** — coherent surfaces and recent text fixes, repeated category art and limited character;
- feedback visibility: **unscored** — these stills do not establish timing, touch response, movement or sound.

The craft skill's 3D lighting recipes are not applicable to the current Canvas presentation. Its principles of an explicit palette, consistent treatment and visible response are useful. No live “floor” verification was run because this is a planning task and another owner controls the Editor.

## 3. Owning-source facts and inventory boundaries

Read the current code before budgeting. [ChapterData](../../Unity/Assets/Scripts/Content/ChapterData.cs) creates **two eras × six families × two paid upgrades = 24 paid constructions**. Each era also has three local stage definitions per family: 36 catalog stage records are not 36 distinct pictures. [ChapterRules.Stages](../../Unity/Assets/Scripts/Content/ChapterRules.cs) derives ownership; [RenderYard](../../Unity/Assets/Scripts/Runtime/JunkGame.Yard.cs) maps absolute levels to paid visual stages:

| Era/local ownership | Absolute family level | Visual state | Art event |
| --- | --- | --- | --- |
| Junkyard 0 / 1 / 2 | 0 / 1 / 2 | 0 / 1 / 2 | initial / paid I / paid II |
| Workshop Yard 0 / 1 / 2 | 3 / 4 / 5 | 2 / 3 / 4 | carry existing paid II / paid III / paid IV |

Therefore budget **six × five = 30 persistent family states**, including six initial sites and 24 earned appearances. Free era entry and story acknowledgement add no paid detail. A “two eras × three unique pictures” production request would misrepresent this contract. The complete [persistent yard rule](../GAME_RULES.md#persistent-construction-yard-2026-11-03) and its complete STATUS handoff were read, alongside the later yard-navigation handoff.

| Family | State 0 | Paid I | Paid II | Paid III | Paid IV |
| --- | --- | --- | --- | --- | --- |
| House | ruined home/site | repaired home | completed home | fitted fence/porch | bench/table/planters |
| Workshop | salvage site | workbench | tools/storage structure | covered work area | assembly fixture/cabinet |
| Recycler | salvage site | sorting bins | salvage rack | partitions | accessible ramp/station |
| Pigeon Loft | salvage site | perch/shelter | fitted loft | address/route fixtures | reinforced perches |
| Cart | salvage site | body/wheels | loading rails | handle/storage | organised hauling crates |
| Power Source | salvage site | assembly | protective housing | cable/control fixture | housed service unit |

These are existing visual meanings, not proposed mechanics. Preserve the House's historical lamps/neon/annex attribution; do not present them as rewards from another family's ownership. Equipment upgrades are separate resource tracks, **not another set of paid construction stages**.

Other verified scope:

- Four ordinary symbol IDs: **Potato, Box, Trash, Pigeon**. Serialized `reelSymbols` refer to four subassets of `Art/PrototypeSymbols/prototype-symbols-v1.png`; Arcade and Parcels reuse these references.
- Memory has **six cards / three pairs**, drawn from symbols 0, 1 and 2; Sorting has four categories and eight items. [ArcadeRules.Shuffle](../../Unity/Assets/Scripts/Core/ArcadeRules.cs) owns the board definitions. Do not invent a fourth Memory pair.
- Parcels has **six physical tokens, four assignment positions, two requests**. Four fixed board definitions reuse the same symbol family; they do not require four new art packs. [Parcels rules](../GAME_RULES.md#pigeons-parcels-2026-11-03) retain separate Start, Finish and Claim.
- [ExpansionCatalog](../../Unity/Assets/Scripts/Core/ExpansionRules.cs): five modules; three support slots with slots 2/3 unlocks; six palette themes including the default; six equipment tracks with two purchasable levels; two Potato Yield levels; two Workbench levels; three Bonus focus choices; three support presets. Palette themes are colour treatments, not six new worlds.
- [ArcadeCatalog](../../Unity/Assets/Scripts/Core/ArcadeRules.cs): slot upgrade levels 0–3, resource packs and a next-bonus boost. Level badges and reused resource art cover these; no new machine mechanics are proposed.
- Six feedback currencies are Coins, Diamonds, Parts, Potatoes, Energy and Tickets in [RewardFeedback](../../Unity/Assets/Scripts/Runtime/JunkGame.RewardFeedback.cs). A resource Potato may reuse the ordinary Potato silhouette with a context frame.
- The serialized Canvas uses **390×844**, Scale With Screen Size, match **0.5**, Reference Pixels Per Unit **100**. The acceptance outputs are 1080×2340 and 1080×2280. These output sizes are not the Canvas reference resolution.
- Project settings name Unity **6000.3.21f1** and uGUI **2.0.0**. GraphicsSettings and all inspected quality pipeline references are null; no URP package is declared. This is source evidence for the existing pipeline configuration, not a live engine probe. Do not introduce URP/bloom as a prerequisite.
- `Resources/house-stages.png` is already deleted in this working tree. `RenderYard` detects an unavailable sprite texture and uses `JtnProductIcon` fallback. The inspected root shows that fallback. This is not permission to restore a deleted asset or overwrite the three retained editable sprite records.

## 4. Fresh research: useful evidence, disagreement and age

All links were checked on the research date. Reddit absolute dates below come from indexed dated results; direct pages sometimes expose inconsistent relative labels such as “hours ago.” Treat those relative labels as unreliable. No popularity count is used as a quality metric, and contributor professional credentials were not independently verified.

| Source and date | Useful evidence | Limit and decision for this project |
| --- | --- | --- |
| [One Artist. Deadlines. A Lot of Art](https://www.reddit.com/r/GameArt/comments/1xbsirh/one_artist_deadlines_a_lot_of_art_how_i_created/), 21 Oct 2026 | A self-described small-studio artist compares styles, fake screenshots, outline thickness and actual engine readability, then tackles UI as part of the same identity. | One production account, not a controlled cost study. Adopt early style comparison and in-context checking; do not adopt its months/content count as our schedule. |
| [The AI Trick for Consistent 2D Game Assets](https://www.reddit.com/r/aigamedev/comments/1xavthu/the_ai_trick_for_consistent_2d_game_assets/), 20 Oct 2026 | Create an object within a screenshot, then isolate it. Comments identify perspective mismatch; the author agrees the method is imperfect. Other participants use explicit references and specifications. | Useful experimental composition step, not a consistency guarantee. The magenta-background extraction recipe is unnecessary when real alpha works and can cause edge contamination. Use approved original context, then verify the isolated result. |
| [Mobile UI readability and information density](https://www.reddit.com/r/UXDesign/comments/1w0kiy8/how_do_you_keep_mobile_ui_readable_without/), 28 Aug 2026 | Participants suggest timed tasks, real phone checks, stable information zones and separate visible art/hit areas. | Small landscape-game discussion; transfer the testing method, not its layout. Android's own 48dp guidance independently supports target sizing. |
| [How do you keep AI-generated 3D game assets consistent?](https://www.reddit.com/r/aigamedev/comments/1xuyv3p/how_do_you_keep_aigenerated_3d_game_assets/), 13 Nov 2026 | Concepts, a style bible and assembled-set review recur; the original poster reports manual Blender cleanup after generation. | About Meshy/3D, not a GPT Image benchmark. Contains a declared sponsored reply and self-promotion in indexed comments. Exclude those product recommendations; retain only the testable workflow hypothesis. |
| [Introducing Images 2.6 discussion](https://www.reddit.com/r/ChatGPT/comments/1xo3oir/introducing_images_26/), 4 Nov 2026 | Mixed reactions include unwanted texture after edits, reference drift and unsuccessful character animation; one user reports useful game-asset results. | Uncontrolled prompts, access and output samples; cannot establish improvement/regression rates. Add edit-drift and animation checks; verify product capabilities in official docs. |
| [I removed all AI assets, before/after](https://www.reddit.com/r/IndieGameDevs/comments/1xrdgs1/i_removed_all_ai_assets_from_my_game_beforeafter/), 9 Nov 2026 | Handmade replacement still draws consistency/prototype criticism; other comments prefer different versions. | Strong reminder that origin is not an art-quality score. Self-reported early-access project and subjective comparison, not a verdict on every AI workflow. |
| [Text readability in low-resolution pixel art](https://www.reddit.com/r/gamedev/comments/1uq7mb5/how_to_solve_text_readability_issue_in_lowres/), 7 Jul 2026 | Comments favour readable mixed-case/body typography even where gameplay is pixel art. | No universal “pixel UI is bad” conclusion. For JTN, retain live TMP text and avoid forcing a pixel font to match temporary symbols. |
| [ControlNet Canny game-assets guide](https://www.reddit.com/r/aigamedev/comments/1jz24v7/), 14 Apr 2025 | Shows a structural-control approach in a different model family. | Provider promotion and older model-specific controls. Canny/LoRA/seed controls are not available in this built-in imagegen schema; do not prescribe them or install another model. |

The evidence supports a practical working position: approved examples, constraints, set review and manual cleanup are useful. It does **not** establish a professional consensus that GPT can autonomously deliver a complete production-ready mobile art set. No independently verified shipped game using this exact GPT Image/tool/Unity combination was found in the sampled sources.

### Professional reference analysis

There is support for studying existing visual solutions. Environment artist **Bastian Pastoors** describes collecting several real-object references, finding recognisable common features, blocking main masses and refining without losing readability in [Bringing Style into Environment, 25 Apr 2017](https://80.lv/articles/bringing-style-into-environment). **Angelo Ciervo** describes boards, material/light references, consistent proportions, blockout and reusable architectural parts in [Turning 2D Concept Art into a Detailed Painterly 3D World, 11 Aug 2026](https://80.lv/articles/turning-2d-concept-art-into-a-detailed-painterly-3d-world). These are first-person artist breakdowns; their rendering tools are not a prescription for JTN.

**Inference:** a visual teardown is a useful design method here. The sources support reference analysis and reconstruction of principles; they do not establish that every specialist recommends “reverse engineering” as one named universal process.

Use the term precisely: measure hierarchy, shape, scale, perspective and response to learn how a visual problem was solved. Copying a recognisable layout/character/material package is a different activity. Ripping textures, models or sprites from another game's files is outside this plan. Published screenshots are research references, not the asset supply.

## 5. Reference board and teardown method

Use a small board organised by **problem**, not a wall of attractive pictures. Suggested maximum: 12–18 reference panels, with 2–3 examples per relevant problem. Each entry needs creator/game, source URL, date accessed, the useful property, the feature to avoid copying and its intended JTN application. Keep a separate provenance/licence note for any material eventually intended for incorporation.

The following official published visuals were inspected in a background browser. These are static marketing/reference views; no game was installed or played.

| Reference | Specific feature to study | JTN translation and limit |
| --- | --- | --- |
| [Hay Day official page](https://supercell.com/en/games/hayday/) and its [published farm screenshot](https://supercell.com/images/32e8c9bae8508f7e03d245e466a206ff/Hay_Day_iPhone_image_V3.png) | Distinct roofs, wooden rails, crops, routes and object masses share an elevated view. Even with many objects, large colour masses identify areas. | Borrow the discipline of functional silhouettes and material differences. JTN's yard is much smaller; omit farm density, expansive roads and Hay Day's particular buildings. |
| [Monument Valley official case study](https://ustwo.com/work/monument-valley/) and [published image](https://ustwo.com/_astro/monument-valley-02_1mDYJr.webp) | White character against quiet teal layers; coral pedestal establishes a focal plane with few details. | Study value separation and empty space around a focal object. Do not impose impossible architecture or a nearly empty puzzle scene on the six-family yard. |
| [MONOPOLY GO! official gallery](https://www.scopely.com/en/games/monopoly-go) | Portrait promotional frames group each verb around a large focal object; BUILD/ROLL/COLLECT are visually distinct. | A reference for staging action and reward attention, not a ready-made skin. Gallery artwork cannot prove touch feel, frame timing or unobstructed live HUD behaviour. Do not copy its board, mascot, fonts, promotional banners or saturation everywhere. |

For original material references, collect useful wood joinery, reused enamel, corrugated metal, small courtyards, repair benches, potato crates and pigeon posture. Prefer the user's own photographs or clearly documented reusable photographs when they will be supplied to a generator. Search references for how an object works, rather than using another game's finished object as the only anchor. A pigeon does not need a hat or a dozen accessories to have character.

### A practical teardown sheet

| Layer | What to measure/record | Result carried into the JTN style contract |
| --- | --- | --- |
| Screen/grid | HUD/world/reels/action/footer fractions; margins; occupied/quiet areas | Mark the existing root screenshot; keep controls clear before adding art. |
| Hierarchy | First three things noticed in 3 seconds; value groups in grayscale | Primary action, current project and symbol outcome remain distinct. |
| Mass and scale | Roof/body/base proportions, relative family heights, large/medium/small shape rhythm | One footprint guide and a family size chart. |
| Silhouette | Black-filled outline at actual use size | A shape signature for each family and symbol. |
| Perspective | Parallel ground axes, verticals, visible roof depth, camera elevation | One ground grid and canonical building box; no mixing front view and steep isometric views. |
| Light | Key direction, shadow length/softness, warm/cool relationship | One light arrow and shadow swatch, shared by all sprites. |
| Materials | Edge shape, roughness cues, damage placement, colour/value relationships | Wood grain follows boards; bent metal retains seams; cardboard folds/tape explain the Box. |
| Detail rhythm | Count strong contours versus small marks, identify focal details | Large forms first; one readable identifying detail at small size. |
| States | How locked/selected/ready/busy/owned are distinguished | Label + shape/border; colour is supplementary. |
| Touch response | On a later authorised live review, sample down/up/cancel and response time | Separate press response, confirmed transaction and celebratory reveal. Static screenshots cannot answer this row. |

After direction review, make **one original anchor scene and one object/material sheet**. Include approved House, symbols, pigeon and a typical UI frame. Those anchors become repeatable input images; the reference board remains explanation and research. A style anchor is an approved concrete example, not a list of adjectives or another game's screenshot.

## 6. Three coherent art directions

### A. The Neighbourhood Workshop — recommended

**Promise:** this place becomes cared for through visible repairs. Small imperfections explain use and ownership. The playful element comes from bent boards, repaired joints and the pigeon's attitude.

- **Palette proposal:** ink `#193B3D`, plaster `#F3E5C7`, weathered wood `#A86A42`, muted teal `#5B9290`, leaf `#7E9865`, amber action `#F2B653`, cyan light `#72D5D4`. Error/shortage uses a reserved rust `#B8493F` with text/shape support. These are starting tokens to test, not an implemented theme or measured contrast pass.
- **Forms:** rounded rectangular masses with purposeful asymmetry; tapered plank ends and a few oversized joints. Clear roof, wheel, perch and bin profiles. Avoid inflating every object into the same toy blob.
- **Materials:** matte painted metal, end-grain wood, cardboard tape, soil and leaf; two or three value planes per material. Place wear at edges and contact points. Keep medium surfaces calm instead of adding speckled texture everywhere.
- **View:** illustrated orthographic three-quarter yard. Proposed ground axes ±30° in the drawing; keep verticals upright and the same top-plane depth across objects. This is an art grid, not a request to rotate the Unity camera or implement isometric mechanics.
- **Light:** one broad warm key from upper left, cool teal shadow sides, short soft cast shadows toward lower right. Cyan belongs to small fitted lights/signs, never a uniform full-screen glow. Bake appearance into art; keep selection/feedback overlays separate.
- **Typography:** retain the current Liberation Sans/TMP during the first slice. Use its regular/bold weights with a disciplined hierarchy and tabular-like stable number allocation where possible. Consider a licensed rounded display face only for short headings if the original mockup demonstrates a clear gain; no font installation is needed now. Prices/body text remain a clean sans, never painted into images.
- **GUI:** cream enamel/paper panels, ink text, amber primary confirmation/action, quiet teal secondary controls. Restrict physical texture to occasional header/corner details. One border weight and a small radius family; icons share apparent stroke weight. Existing routes and title/price grouping remain.
- **Feel:** immediate visual press response, with subtle compression only if useful; keep frequent navigation immediate. Confirmed construction may receive a restrained local dust/shape reveal within its existing lifecycle. Potato squash or pigeon blink/head-tilt are optional contextual accents, not idle loops or a requirement on every outcome. Reduced presentation still communicates the same ownership/result without flights. Sound/haptics are separate later implementation decisions.

References: Hay Day's functional object grouping and Pastoors' recognisable-object analysis, with Monument Valley's value restraint. The palette, repairs, local props and pigeon shape must be JTN's own.

### B. Enamel Yard Theatre

An illustrated cut-paper/enamel sign world: cream `#F5E8CA`, bottle green `#244C45`, rust `#BC603F`, petrol `#347C80`, yellow `#E9B74F`, cyan `#8DE0D6`. Flat front/three-quarter shapes use one stepped shadow and consistent dark contours; slight cut-edge irregularity replaces surface rendering. Text uses clean TMP body and bold compact headings. GUI frames resemble simple labels and repair tags; motion is hinged/slide/pop, not glossy elastic jelly. Easier to reproduce and manually repair than A, but it risks reading as a board/menu unless the courtyard composition and building silhouettes are strong. Study Monument Valley's reduction of detail and the artist breakdowns' form analysis, not their scenery. Good fallback if A fails at small size or cleanup cost.

### C. Miniature Salvage Diorama

A consistent low-poly, bevelled 3D source kit rendered offline to transparent sprites, or later real-time 3D if separately justified. Palette: slate `#253B46`, warm ivory `#EFE0C2`, terracotta `#B57451`, sage `#879A73`, mustard `#D9AD4B`, turquoise `#69C7C4`. Real joins, matte materials, fixed orthographic camera and broad soft lighting; simple clean GUI and the same TMP body. Rigid props and light are highly repeatable from one source scene; stage changes are modular additions. Higher modelling/setup cost and more pipeline work. No conversion to real-time 3D, Blender install or new render pipeline is authorized. Study Hay Day's volume/readable function, without reproducing its buildings or proprietary models.

### Medium tradeoff for this project

The following are planning estimates and engineering judgments, not benchmarked timings.

| Route | Main work | Repeatability | Fit for existing JTN |
| --- | --- | --- | --- |
| Painted 2D with shallow volume, A/B | illustration/cleanup, consistent stage edits, a few movable layers | Moderate; requires anchor review and corrections | Best initial fit: Canvas, fixed view, small yard; no renderer migration. |
| 2.5D rendered sprites, C | original mesh kit, fixed source camera/light, render and clean exports | High for scale, perspective and state families once set up | Viable if a modeller is available; retain sprites/uGUI in runtime. Budget roughly 30–60 extra setup/model hours before any later savings; validate with one family. |
| Real-time 3D yard + uGUI | modelling, lighting, integration, sorting/occlusion, performance and device QA | High geometric consistency, higher runtime complexity | Defer. Roughly 60–120+ added integration/setup hours is a planning allowance, not a quote. The current problem does not require moving cameras or physical simulation. |

## 7. Testable art acceptance and rejection

Passing technical checks means an asset is usable; it does not mean the design has appeal. Assess the set in a real layout, then ask whether it makes the yard recognisable and worth improving.

All numeric visual tolerances below are **proposed production gates**, to calibrate on the first slice. They are not measured current defects or engine requirements.

| Criterion | Accept | Reject / next correction |
| --- | --- | --- |
| Family recognition | In a brief unlabelled six-object sheet at intended display size, at least 4 of 5 reviewers identify each family from its function; record guesses | A Workshop only identifiable because of its caption; change main masses before detailing. This small test is qualitative, not statistical validation. |
| Four symbol separation | 4 of 5 reviewers distinguish all four at the smallest actual puzzle size and in grayscale | Box/Trash/Parts share a blob; alter silhouette and value grouping. |
| Stage readability | Every paid adjacent transition has one named visible structural change at target size; reviewers can order a five-state family strip | More glow/texture is the sole upgrade; paid details appear before ownership. |
| Perspective | Canonical ground axes within about ±3° of the anchor guide; shared upright verticals/top planes | Front-facing crate beside steep isometric house; redraw/reproject, do not hide with shadow. |
| Ground registration | Same approved footprint/pivot; stage-to-stage ground point shifts no more than 2 final display pixels during a static overlay comparison | Building floats, hops or grows from its centre when stage swaps. |
| Scale | Doors, pigeon and common props agree with the approved relative-size chart; no unexplained size jump between state exports | New cabinet is larger than the Workshop roof; correct proportions. |
| Light/material | Key and shadow direction match anchor; wood/metal/cardboard remain distinguishable without tiny texture | Conflicting highlights, random plastic gloss, impossible material seams or noisy “detail”. |
| Alpha | Real varying alpha; fully transparent exterior; no cut frame, white matte or fake checkerboard; inspect over cream, navy and magenta | Merely saving an RGB picture as RGBA, halos, a rectangular haze, missing thin rails. Fix matte/edges manually or regenerate once with a precise cause. |
| Screen hierarchy | In a 3-second glance, reviewers find SPIN, current project and outcome; no competing animated decoration | Everything saturated/emissive or every card has a glowing badge. Reduce competing emphasis. |
| States | Locked, available, selected, owned/max, busy, saved and ready are understandable using labels and shape/border, including grayscale | Colour-only availability; “selected” looks purchased; inspection appears to spend a currency. |
| Text | Exact labels, numbers and units remain live TMP, with no clipping at both portraits and long existing labels/large existing balances | Generated lettering; shrinking all body text to make new art fit; broken grouped title/price units. |
| Touch | Preserve existing hit regions; later verify topmost raycasts and physical size. Android baseline is 48dp, not 48 screenshot pixels | A tiny visible object gets a tiny target, decorative art intercepts taps, or adjacent family targets overlap. |
| Character | Reviewers can describe one distinctive, repeatable trait of the pigeon/yard without being prompted | Generic “cute mobile game” with no recognisable design; no image defects but no memorable choices. Redesign, rather than adding texture. |

[Android's official guidance](https://developer.android.com/guide/topics/ui/accessibility/views/apps-views#use-large-simple-controls) recommends 48×48dp targets and permits smaller visible artwork inside them. Unity reference units, screen pixels and Android dp are different; validate density on an actual requested target device later. For planned text colours, use the [Android contrast guidance](https://developer.android.com/guide/topics/ui/accessibility/apps#increase-text-visibility) as a baseline: 4.5:1 normal text / 3:1 sufficiently large text. Check the actual state/background pair, not only palette swatches.

Use a six-axis board after the slice: palette, uniformity, depth/outline, silhouette, UI and observed feedback. Require no axis below 3/5 and no hard technical rejection. Keep one sentence of frame evidence per score. Aesthetic approval remains a human judgment; no model's “93% similarity” score substitutes for it.

## 8. Asset inventory and reuse plan

Priority **P0** is the slice, **P1** completes the current game, **P2** is later polish. Counts are planning units; a state assembled from layers is not necessarily a separately generated bitmap. The inventory follows the existing game; it adds no new economy or progression.

| Group | Required scope / variants | Representation | Priority and owning surface |
| --- | --- | --- | --- |
| Yard composition | one ground layout, one quiet background; modular boundary/vegetation/salvage clusters, initially about 6–10 reusable pieces | bitmap ground/backdrop + optional code/mesh path and ground shadows | P0 composition; P1 dressing. `JunkGame.Yard`, `JtnYardGraphic`. Keep six hit regions and route badges clear. |
| Construction families | **30 approved states**; six strips of initial + paid I–IV. Produce House five first; remaining 25 later | bitmap composites or reusable bitmap layers; C alternative uses original source meshes rendered to sprites | P0 House; P1 five other families. Same footprint/pivot within each strip. |
| Ordinary finds | **4 master symbols**; canonical Potato face, taped Box, tied Trash bag, alert Pigeon | bitmap; 256–512 working exports depending actual size; source masters larger | P0. Shared reels, Sorting, Memory and Parcels; do not generate independently for each screen. |
| Resource/currency signs | six roles: Coin, Diamond, Parts, Potato, Energy, Ticket; Potato reused, about five new masters at most | Prefer deterministic vector/code small HUD signs; optional matching bitmap medallion for large reward/card use | P0 core HUD; P1 other contexts. Never bake amounts into art. |
| Modules | **5** identity illustrations; installed/uninstalled/owned states are frame/badge changes | bitmap inserts; code borders/statuses | P1. Salvage/Packing/Harvest/Combo/Dynamo. Keep actual names/effects from catalog. |
| Equipment | **6 family thumbnails**, usually derived from construction art; level 0/1/2 badges, 12 paid steps | reused bitmap crops + code badges | P1. No additional buildings or invented functional attachments. |
| Support slots/presets | three slot locations; locked/unlocked/empty/installed; three preset labels | vector/code sockets and state overlays, existing module bitmaps | P1. No need to generate nine different socket images. |
| Bonus Reel / Workbench / focus | one reel insert; Workbench 0/1/2 representation if useful; three focus symbols; boost indicator | reuse Box/Parts/Potato/Energy plus code bezel/level/focus state; optional one workbench art with two approved attachments | P1. Do not confuse Workbench level with Workshop construction state. |
| Appearance themes | default Neon, Amber, Mint, Violet, Frost, Rose | code palette swatches/tints | P1 theme parity. Test contrast across six themes; avoid six duplicated atlas sets. |
| Potato Yield / trades / resource packs | two yield levels; Parts/Potato exchange; packs and next-bonus boost | reused resource icons + code quantities/arrows/level tags | P1. No art per price or quantity. |
| Memory | six card instances, one common back, three shared face types, selected/mismatch/matched states | one bitmap back if it adds identity; existing symbols + code frames/flip | P1. No six unique generated faces or generated text rules. |
| Sorting | four category containers and current item | shared symbol art + deterministic bin frames/labels; optional four modest bin skins | P1. Reuse signs, preserve category identity. |
| Parcels | one quiet delivery-board motif, A/B zones, six tokens, four positions; selected/placed/empty/met/not-met/ready | reused symbols, code slots/labels/markers; at most one decorative board header | P1. Stable IDs and names remain visible; no fake inventory consumption. |
| Orders, skills, milestones, chapter guidance | existing data-driven cards; family/resource/category motifs | vector/code nodes and connectors; reuse thumbnails; optional chapter vignette only after slice | P1 existing screens, P2 vignette. Do not add rewards/characters/story beats through art. |
| Product chrome | header, panel, card, primary/secondary buttons, tab selected marker, badge and tooltip | existing procedural/vector/code UI; about 6–10 reusable visual primitives, optional 9-slice texture details | P0 base style / P1 all screens. Avoid full-screen image skins. |
| Profile / Settings / information | existing icons and text, useful selection/disabled states | deterministic glyph family, TMP and existing surfaces | P1. No generated Settings screenshot as runtime UI. |
| FX and life | optional pool of at most about 4–6 reusable candidates: local dust, sparkle, reward trail, construction settle, pigeon blink/head tilt, symbol pop; each must justify purpose and frequency | code/mesh particles and short transform/layer animation; a small bitmap particle set only when necessary | Basic press/cancel/reduced behaviour is P0; extra effects are P2. Zero additional effects is acceptable. Keep exact credited amounts. |

Expected initial envelope: roughly **55–80 distinct art masters/composites**, depending reuse and whether bins/workbench receive separate paintings, plus reusable code UI. This is not 55–80 generator calls: variants, rejected candidates and edits increase calls. Start with a manifest, recount after the slice and remove optional assets that do not improve the actual screen.

**Do not generate:** prices, names, body text, counters, progress bars, touch targets, simple arrows/locks/checkmarks, flat rounded panels, layout, consent states, hit masks, packing atlases, source meshes, or arbitrary extra items to fill a sheet. These are existing data/code, deterministic authoring, or a separate modelling job.

## 9. Current GPT Image capabilities and actual tool boundary

OpenAI announced **Images 2.6 on 4 Nov 2026**, including API IDs `gpt-image-2.6-sunburst` and `gpt-image-2.6-flare`, and availability in ChatGPT/Codex. Sunburst is presented for detailed editing, Flare for faster iteration. These are provider descriptions, not measured JTN performance. [Official announcement](https://openai.com/index/introducing-images-2-6/), [Sunburst model card](https://developers.openai.com/api/docs/models/gpt-image-2.6-sunburst).

| Surface | Verified capability / limitation | Production consequence |
| --- | --- | --- |
| Public Image API | Text/image generation and edits; 2.6 adds complexity control, markup-guided edits, SVG and GIF outputs. [Current guide](https://developers.openai.com/api/docs/guides/image-generation#gpt-image-26) | These features exist publicly; they are not all arguments of this session's tool. No API access or account capability was tested. |
| Built-in `image_gen` in this session | Schema exposes prompt, transparent-background flag and either local reference paths or up to five recent images. It exposes no model selector, numeric size, quality, seed, mask, complexity, GIF or SVG selector. | Use built-in for later authorised raster work. Request framing in prose, then inspect returned size/alpha. Do not claim a proven model pin, exact dimensions, seed reuse or hidden controls. |
| Local imagegen skill | Prefers built-in; CLI fallback needs explicit user choice. Its fallback text still defaults to GPT Image 2 and says that model has no transparency. | This is documentation drift: official docs now describe GPT Image 2 alpha preview and newer 2.6. Do not edit global skills, switch models or choose fallback because the old text is convenient. |
| API dimensions | Standard 1024×1024, 1536×1024 and 1024×1536 are documented. The custom-size section explicitly describes 2/2.5: multiples of 16, ratio ≤3:1, max edge 3840 and 655,360–8,294,400 pixels; >2560×1440 is experimental. [Generate reference](https://developers.openai.com/api/reference/resources/images/methods/generate) | The newly added 2.6 guide still contains older 2.5 examples. Treat arbitrary 2.6 size parity as a documentation gap until clarified/validated in an authorised pilot. Do not send 1080×2340 as if it were an accepted API size; use an intermediate canvas and compose/resample outside the generator. |
| Alpha | Official examples request transparent PNG/WebP and actually inspect alpha. Prompts describing a backdrop can conflict with transparency. [Transparency cookbook](https://developers.openai.com/cookbook/examples/multimodal/transparent-image-assets-for-campaigns-and-presentations) | Ask the built-in tool for true transparency; inspect pixels and composites. The cookbook's retained examples are historical GPT Image 2 assets, not a measured 2.6 benchmark. |
| References / controlled edits | Input images support editing; masks guide changes rather than guaranteeing pixel-exact preservation. [Edit reference](https://developers.openai.com/api/reference/resources/images/methods/edit), [mask guidance](https://developers.openai.com/api/docs/guides/image-generation#edit-an-image-using-a-mask) | Use approved originals and explicit invariants. Changes outside a requested region require manual review. Do not infer dedicated mask/markup support in the built-in tool. |
| Text/layout/consistency | Official limitations still include precise text placement, exact layout and recurring-identity consistency. [Limitations](https://developers.openai.com/api/docs/guides/image-generation#limitations) | Live UI remains code/TMP. References improve control but are not a contract of exact geometry or sequence identity. |

No generated output in this research establishes which backend the built-in tool uses. The accurate statement is “OpenAI publicly offers Images 2.6; this tool's backend ID is not exposed.” If exact model pinning later becomes essential, resolve it with the user before changing the execution path. Do not substitute another provider/model, or silently use an older GPT Image model for alpha.

**Sprite sheets:** a regular-looking grid is not a guarantee of identical cell bounds, pivots or correct state ordering. Generate candidates individually, then pack deterministically after approval. **Tileability:** a seamless prompt is a request, not proof; test a 3×3 repeat for seams, lighting drift and obvious repetition. This yard can mostly use finite modular pieces instead of generated tiling. **Animation:** API GIF output does not establish riggable layers, stable topology or game-ready timing. The current built-in schema has no GIF control; favour authored transform/layer animation and separately checked frames.

## 10. Production workflow after explicit production authorization

### 10.1 Freeze the approved contract

Record direction version, palette roles, canonical view/ground grid, light arrow, scale chart, key silhouettes, target RectTransforms, current theme behaviour and acceptance gates. Use the current root screenshot as a **layout constraint**, not as the style reference that preserves its placeholder look.

Create a manifest with fields: `asset_id`, family/symbol ID, paid visual stage, intended surface, source dimensions, content bounds, ground/pivot coordinates, intended display rectangle, style revision, source/parent files, prompt, reference roles, actual tool/model if reported, alpha check, editor/platform import settings, reviewer decision and reason. A small Markdown/CSV table is sufficient initially; no asset-management service is required.

### 10.2 Establish original style anchors

Prepare one root composition and one House/material/symbol anchor. Later generation of these anchors requires the user's direction choice. For the first exploration, compare **two or three coherent candidates**, not dozens of unrelated mood images. Review at the actual screen proportions. Choose a single master before producing family variants.

References sent together should have explicit roles: image 1 = original approved style; image 2 = exact object/stage edit target; image 3 = layout or footprint guide. Avoid sending a broad collage of conflicting game styles. With local targets, inspect them first; use local reference paths for all targets when supported. Do not mix that mechanism with the recent-image mechanism. The session schema caps recent images at five; keep the smallest useful set, usually 2–3.

### 10.3 Produce by family, with bounded iteration

1. Draft House state 0 and paid II, plus the four symbols, in context. Compare two or three candidates for the difficult identity decisions.
2. Approve one House shape and one symbol treatment. Derive states I, III and IV with controlled edits and an explicit list of additions/retained parts. Compare every state against the same approved base, not only against the immediately previous generation.
3. Re-isolate any contextual object on genuine alpha. Compare its silhouette, scale and light to the contextual image. If extraction restyles it, manually isolate from the accepted source or correct a narrow region.
4. Make the full five-state strip with deterministic assembly. Add separate shadows/selection overlays in authoring where needed. Do not ask the generator to create the packed production atlas.
5. Give a family an initial maximum of three candidate attempts and two focused correction rounds per selected image. If a specific recurring flaw survives, switch to manual drawing/compositing or simplify the style. This is a proposed work/cost limit, not automated enforcement.
6. Finish and review one coherent slice before batching other families. Record rejected candidates and the reason; do not rename the last generation “final” automatically.

### 10.4 Resolution, framing, scale and pivots

- **Working sources:** normally 1024 square for objects, landscape/portrait as needed for composition. **Proposed final exports:** 256–512 for symbols, 512–1024 for family composites, 1024–2048 for finite backdrop pieces. These are starting ranges, selected against measured display coverage; they are not claims about the built-in tool's exact output controls.
- Keep roughly **10% empty margin on each side** of an isolated object and a shared baseline. For a 1024 family canvas, propose a ground contact at `(512, 840)` in top-left pixel coordinates and a maximum footprint width of 720 pixels. The art director may adjust once for the slice, then lock it for that family strip. Shadows may use a separate layer.
- Annotate actual content bounds after rendering; a prompt cannot ensure exact occupancy. Do not auto-trim each stage independently and lose registration. Export all stages into the same family canvas or preserve the exact untrimmed transform in metadata.
- The existing House uses special per-stage widths and pivot origins, with y `0.1409147`; replacing it with a generic bottom-centred sprite without updating the presentation contract would make it jump. The integrator must choose either compatible exports or one intentional migration of all five states, preserving layout and hit areas. That code change belongs to the later slice, never this research.
- Keep source originals and editable layered masters. Resampling, cropping, palette corrections and alpha repair produce new derived exports; they do not overwrite the generation original.

### 10.5 Alpha and visual QA before import

Verify the file really has an alpha channel with both transparent and opaque/content samples. An all-255 alpha channel is a failure. Check expected empty margins and opaque interiors; do not demand opaque glass or every antialiased edge. Composite over light cream, dark ink, a saturated contrasting colour and checkerboard. Inspect at 100% source and intended displayed size for dark/white fringes, clipped thin elements, disconnected highlights and rectangular residue.

Then inspect black silhouettes, grayscale value separation, the five-state overlay/strip and the assembled root. Check the symbols at the smallest Parcels/Sorting usage as well as the reels. Test two portraits without scaling a whole screenshot non-uniformly. Compare long actual names such as Pigeon Loft and Bonus Workbench; generated drawings do not justify shrinking the established type.

### 10.6 Naming, provenance and retention

Proposed paths for the later production phase, to approve with the slice:

```text
art_source/jtn/<style-revision>/originals/
art_source/jtn/<style-revision>/masters/
art_source/jtn/<style-revision>/manifest.csv
Unity/Assets/Art/Jtn/Yard/
Unity/Assets/Art/Jtn/Symbols/
Unity/Assets/Art/Jtn/UI/
Unity/Assets/Art/Jtn/Atlases/
```

Example: `jtn_house_s03_a01_v003_source.png`, `jtn_house_s03_v003_export.png`; `s03` always means paid visual state 3, not era-local stage 3. Symbol names follow `potato`, `box`, `trash`, `pigeon`. Store parent version and prompt with each accepted export; keep stable asset IDs when a file revision changes.

Built-in outputs initially live under Codex's generated-image area; selected project assets must later be copied into project-owned paths, without overwriting unrelated files. Originals, selected masters and acceptance receipts are durable. Disposable previews/logs belong under `.work/art-production-<date>/`; proposed manual seven-day retention with a 256MiB inspection threshold. Check C:/X: space and actual generator output locations around batches. This is not an installed cleanup or rotation mechanism; never remove source assets or old receipts to meet a preview budget.

## 11. Paste-ready prompt templates — do not execute during research

Fill placeholders from the approved contract. These are prompt text, not claims that all fields are tool parameters. Direction A is a proposal until reviewed.

### Original master composition

```text
Use case: stylized-concept
Asset type: original mobile-game art-direction anchor, not a runtime UI image
Primary request: an inhabited salvage courtyard gradually becoming a cared-for neighbourhood workshop.
Input images: Image 1 is the current JTN screenshot for occupied areas and layout only; its placeholder rendering is not the desired style. Image 2 is our original approved material/shape guide, if available.
Style/medium: painted 2D with shallow illustrated volume, large readable forms, purposeful small asymmetries, restrained surface detail.
View: fixed elevated orthographic three-quarter view; use the supplied ground axes and upright verticals.
Light: broad warm upper-left key, quiet cool shadow sides, short soft lower-right shadows.
Materials: weathered wood with visible joins, matte enamel metal, cardboard folds, soil and a few leaves.
Palette: ink #193B3D, plaster #F3E5C7, wood #A86A42, muted teal #5B9290, leaf #7E9865; amber #F2B653 and small cyan #72D5D4 accents.
Composition: keep the supplied HUD, controls, family hit regions and reel area clear. Show only existing game objects and approved ownership state {STATE_SET}.
Constraints: no readable lettering, prices, counters, fake buttons, extra currencies, logos or borrowed game characters. Do not add an upgrade detail before its owning state.
Review goal: a recognisable place at actual portrait-screen scale, not a detailed poster.
```

### Single family asset

```text
Use case: stylized-concept
Asset type: isolated JTN building sprite
Input images: Image 1 is our approved style anchor; Image 2 is the canonical footprint and ground guide; Image 3 is the approved family identity, if available.
Subject: {FAMILY}, paid visual state {0..4}. Visible structure: {EXACT_ALLOWED_FEATURES}.
Preserve: {FAMILY_IDENTIFYING_SHAPE}, the guide's view, proportions, upper-left lighting and palette roles.
Composition: one complete object, shared ground contact, generous empty margins, no clipped roof or wheels. Match the guide's footprint and scale rather than filling the canvas arbitrarily.
Materials: {MATERIAL_RULES}. Large readable silhouette; one or two identifying secondary details.
Background: actual transparency, no scene, plinth, checkerboard, coloured backdrop or rectangular glow. Keep ground shadow separate if requested by the guide.
Constraints: no text, interface, extra buildings or unearned later-stage details.
```

### Controlled stage edit

```text
Use case: precise-object-edit
Asset type: next persistent construction state
Input images: Image 1 is the exact approved {FAMILY} state {PREVIOUS}; Image 2 is the style anchor; Image 3 is the footprint guide.
Primary request: change only {SPECIFIC_STRUCTURAL_ADDITION} to represent paid visual state {NEXT}.
Keep unchanged: camera/view, object identity, existing roof/body proportions, existing earned parts, ground contact, scale, canvas framing, palette, light direction and transparent exterior.
Do not add: {LATER_STAGE_FEATURES}. Do not replace this with a new building or brighten every surface.
Acceptance focus: the new addition is identifiable at {DISPLAY_SIZE}; the previous structure still registers on the same footprint.
```

### Symbol master

```text
Use case: stylized-concept
Asset type: isolated shared mobile-game symbol
Input images: Image 1 is our approved symbol sheet for visual treatment; Image 2 is our pigeon/material reference where relevant.
Subject: one {POTATO | TAPED BOX | TIED TRASH BAG | ALERT PIGEON}.
Identity: {SILHOUETTE_AND_EXPRESSION_RULE}. Keep the same view and light as the approved family.
Composition: complete centred silhouette with roughly ten percent clear margin; balanced visual weight against the other three symbols.
Style: {APPROVED_STYLE_REVISION}; clear large shapes and sparse meaningful detail. Readable at the supplied smallest puzzle size.
Background: true transparency; no floor, cast shadow, badge, words or decorative frame.
Constraints: no extra objects, fake pixels, gradient haze or tiny detail used instead of a recognisable shape.
```

### Isolation/correction from an original context

```text
Use case: background-extraction
Input images: Image 1 is our accepted original JTN composition; Image 2 marks the exact object to isolate.
Primary request: isolate only {OBJECT}, preserving its accepted silhouette, painted edges, colours, angle, proportions and lighting.
Output: that one complete object on actual transparency, with the agreed ground contact and safe margin.
Keep all earned visible parts. Do not reinterpret, re-pose, rescale relative parts, add details, retain neighbouring objects or create a checkerboard picture.
```

For an edit, inspect the returned result for invariant drift; prompt wording does not guarantee an unchanged region. Use manual cleanup where exact registration matters.

## 12. Future Unity integration contract

These are recommendations for the later implementation owner. No import/settings/scene authoring was performed here.

1. **Keep runtime structure:** retain uGUI/TMP, current navigation, quotes, save logic and family ownership projection. Bind art by family ID + paid visual state; do not branch on wallet, total progress or story ACK to choose a building picture.
2. **Use appropriate representation:** replace only decorative drawing where approved. Keep path meshes, simple backgrounds, small signs and UI primitives procedural where they already work. Decoration uses no raycasts; existing inspection controls retain ownership and geometry. Background layers may not contain paid structures.
3. **Sprite import proposal:** Sprite (2D and UI), Single for individual exports, Full Rect initially, sRGB, alpha from source, Clamp, Bilinear for A/B, Read/Write off, mipmaps off for screen UI unless a measured minification case justifies them. Keep existing pixel prototypes Point-filtered until replaced; do not globally change unrelated textures. PPU **100** initially matches existing art and Canvas reference PPU, but RectTransform sizing remains explicit. [Unity sprite importer](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/textures/textures-reference/texture-type-sprite).
4. **Understand PPU:** PPU describes sprite pixels per world unit; CanvasScaler additionally controls UI density and sizing. Setting PPU alone does not fix content occupancy or aspect ratio. Preserve the 390×844 reference and existing fitting/safe-area logic for the slice. [uGUI 2.0 CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html).
5. **Atlas proposal:** a small built-in V2 atlas for shared symbols/UI art and separate yard atlas pages by actual residency. Start with 2048 pages; do not force all 30 family states, text and background onto one enormous texture. Disable rotation for Canvas sprites; start with rectangle packing and at least four pixels padding, inspect dilation/edge bleeding. [Unity 6000.3 atlas reference](https://docs.unity.com/en-us/engine/6000.3/manual/unity2d/sprite/atlas/sprite-atlas-reference). An atlas can reduce texture switches; it does not guarantee one draw call across different materials, masks or Canvas boundaries.
6. **Author safely:** Unity Editor owner creates importer/atlas/serialized bindings through supported Editor authoring. Do not hand-edit `.meta`, scene YAML or sprite records to bypass authoring. A later atlas tool implementation should read the shipped authoring/runtime examples first; no Addressables/service integration is needed for this small set.
7. **Compression:** start the slice with lossless/uncompressed comparison, then compare Android ASTC 4×4/6×6 only where the chosen device supports it, particularly alpha and thin contours. Keep a supported fallback policy for the eventual target list. A 2048² RGBA32 texture is about 16MiB before mipmaps; two or three such pages consume meaningful memory. Count actual resident atlas pages and original textures; source PNG size is not GPU memory. Compression, overdraw and frame cost need future native profiling.
8. **Text:** retain editable TMP text and existing glyph coverage. A new display font, if approved, needs a licence record, required-script glyph check, fallback check and full short/regular portrait regression. Replacing temporary font appearance is not permission to clear/overwrite the already dirty font asset.
9. **Behaviour:** keep the existing Busy reveal semantics, reload reconstruction and reduced feedback. Construction acquisition already has a saved authority boundary; the art must follow it. Currency flights represent credited receipts once, never inspection/Finish/replay or a displayed “included” subamount twice. Any integration touching consent/payout/persistence/replay requires the focused `jtn-logic-review` check.

### Proposed feel timings

Treat these as tuning hypotheses for a later live slice, not timers to insert into callbacks. Start visual acknowledgement on the first rendered frame after an accepted pointer-down; a small press transform may settle over **100–160ms**, using ease-out. The action still commits only through the existing valid release/Submit path. Restore the presentation promptly on release, cancellation, disable or context replacement; do not delay the callback for the transform to finish.

Use **no added entrance/exit animation** for frequent tabs, repeated catalogue inspection, keyboard navigation or an unchanged wallet redraw. An occasional contextual panel may use an optional **125–200ms** ease-out transition only when it explains spatial origin; if a static change is clearer, use it. A confirmed construction may have a **300–500ms world-art reveal** inside the existing lifecycle; that is separate from utility-panel timing and cannot add a new Busy interval or postpone settlement. Keep normal/fast spin timing and authored reel/result presentation intact. No blocking celebration after every spin, continuous badge pulse or full-screen shake for routine actions.

Retarget any interruptible presentation from its current visible value. Reduced presentation preserves static ownership, final values and explicit text; it need not add a fade when an immediate state is clearer. Samples must show frames advancing plus an observed down/up/cancel or result event; a captured still does not prove timing, feel or input safety. These recommendations draw on the installed Emil skills and their purpose/frequency/response guidance; their browser compositor and CSS recipes are not Unity performance guarantees.

## 13. Implementation waves, ownership and readiness

Roles below are responsibilities, not dispatched agents. One person/agent may fill several roles. **MAIN remains the sole Editor owner**; art preparation can occur outside Unity, with an explicit handoff before integration. No agent or external AI CLI may be started without a separate user request.

Time ranges are productive work hours after authorization, assuming an art-capable operator, the existing mechanics and at most two substantive direction-review rounds. They exclude approvals waiting time, major redraw after changing direction, marketing art, new gameplay, localisation expansion and native packaging.

| Wave | Owner / dependencies | Concrete output and approximate scope | Ready when / verification | Hours |
| --- | --- | --- | --- | --- |
| 0. Direction and reference contract | Art lead; user reviews this proposal | A/B/C decision; 12–18-panel annotated board, original-view/scale guide, manifest skeleton and a concise approved style contract | Chosen direction is explicit; all refs have purpose/provenance; 30-state mapping agreed. Docs/links only; no Unity necessary. | 6–12 |
| 1. Vertical visual slice | Art lead + MAIN integrator; approved direction | Existing root screen in both portraits, House **five states**, four symbols, minimum chrome, one construction response; immediate press/cancel and reduced presentation are part of this slice. About 10–15 selected art/visual components with shared layers | Slice passes art gates and existing root navigation/inspection. Compare start, mixed/selected and paid states; free era entry retains paid II. Test down/up, swipe-out-and-back cancellation, jitter, rapid navigation, context replacement and reduced presentation. Exact labels/costs/normal/fast survive. Run relevant Core or source-matched reuse, zero compilation errors and explicit passing JunkToNeon EditMode; focused private PlayMode for changed surfaces. No APK. | 20–36 |
| 2. Remaining persistent yard | Art lead + MAIN; accepted slice | **25 remaining family states**, reusable ground/background/dressing, six family thumbnails | Each of 24 paid transitions has its expected visible structural consequence; five unrelated families do not change. Verify mixed ownership, free entry, busy reveal, load/recovery and both portraits with private fixtures. Core reuse only with matching inputs, compile/EditMode and scoped UI checks. | 40–70 |
| 3. Collections and product UI | UI artist + MAIN; shared symbols/materials | Five module illustrations, derived equipment thumbs, support sockets, bonus/workbench, yield/trade/pack signs, coherent utility chrome and six palette treatments | Inspect available/locked/owned/max/selected/shortage/ready states and all existing routes. No accidental purchase/claim; theme contrast and long caption fit pass. Mandatory game-change checks plus focused consent review if handlers/state paths touched. | 20–40 |
| 4. Mini-game presentation | UI artist + MAIN; shared symbol set/UI kit | Memory back and face treatment, Sorting bins, Parcels delivery framing; retain rules and existing board/control counts | Preview and active/Ready/saved views; Memory mismatch/Continue, Sorting category response, Parcels token identity/A/B/Finish/Claim/cancel remain distinct. Reload/back and reduced presentation checks. Same focused compile/EditMode/private UI loop; no new economy. | 12–24 |
| 5. Optional motion and final art QA | Art lead + MAIN; integrated surfaces and first-slice interaction contract | Review at most about 4–6 effect candidates; implement only justified effects, plus final size/alpha/atlas review. This is not a quota or the first interaction check | Purpose/frequency table retains or rejects each candidate. Confirmed result/reveal and replay demonstrate truthful feedback. No blocked taps, per-frame allocation surprises or growing effects. Scoped regressions; profile Editor separately from device. | 16–30 |
| 6. Acceptance and cleanup | MAIN + human reviewer; complete slice set | Final evidence matrix, art manifest decisions, unused-task-art review and handoff | Both portrait layouts, early/mixed/complete yard, all six themes, worst actual text/values and reduced effects reviewed. Record human recognition feedback. Native memory/frame-time/touch remain a separate requested device milestone. | 12–24 |

Total planning envelope: **126–236 productive hours**. At six productive hours per day this is approximately 21–40 working days, or **4–8 working weeks** for one experienced person, plus feedback/waiting. Generator latency is a small part of composition, editing, integration and acceptance time. Wave 1 should establish real throughput and revise every later estimate before committing to the complete set.

Each wave should preserve the first failure, name the cause before retrying and stop expanding verification once its relevant gates pass. No mutation campaign or full progression replay is needed to validate an unchanged art-only mapping. Required project acceptance still applies when code/scenes/UI actually change; this research did not run those future gates.

## 14. Cost envelope with explicit assumptions

No content-generation charges were incurred by this research. Built-in subscription/quota consumption and remaining allowance are not exposed here; do not label later built-in generations “free” or equate them with a public API bill.

The [official pricing table](https://developers.openai.com/api/docs/pricing) lists standard GPT Image 2.6 Sunburst/Flare rates per million tokens: **$5 text input, $8 image input, $30 image output**; cached input is listed separately. Both have equal token rates, which does not imply equal tokens per image. A Responses workflow also pays for its mainline model. No cache or Batch discount is assumed here.

For an optional, separately authorized API route, use:

```text
estimated USD = 5 × text_input_tokens / 1,000,000
              + 8 × image_input_tokens / 1,000,000
              + 30 × image_output_tokens / 1,000,000
              + any mainline-model charges
```

**Scenario assumption, not a quoted image price:** 1,000 input text tokens, 1,000–10,000 image input tokens and 1,000–10,000 output tokens gives about **$0.043–$0.385 per candidate/edit** before mainline charges. Actual dimensions, quality and reference count can move usage outside that range. Do not apply older per-image GPT Image 1/1.5 or GPT Image 2 pricing tables to 2.6.

| Scenario | Assumed candidate/edit count | Calculated generation range | Practical planning reserve |
| --- | --- | --- | --- |
| Vertical slice pilot | 40–80 | about $2–$31 | **$5–$40**, subject to actual usage and authorized cap |
| Whole current art set | 200–500 | about $9–$193 | **$15–$250**, with difficult redraws/resolution changes requiring re-estimation |

These are token scenarios, not proof of an attainable rejection rate or a spending authorization. After an authorized 5–10-request sample, use real usage/acceptance numbers to replace assumptions and set a batch cap. The built-in route cannot promise that same API accounting.

Human work dominates. As a purely illustrative labour scenario at **$25–$60/hour**, the 126–236-hour plan corresponds to roughly **$3,150–$14,160**, plus any generation spend. Those hourly rates are assumptions, not researched vendor quotes or a recommendation to purchase services. A solo creator may pay no external labour bill, but still spends the time. Existing editor/paint tools may suffice; no new subscription/asset pack is included. A 3D route adds its own original modelling/rendering work; it is not automatically cheaper because meshes can be reused.

## 15. Handoff, unresolved questions and first small step

Initial research completed the source inventory, nine local visual views, receipt comparison, fresh community/official research, three directions, production/QA/import plan, prompt templates and estimates. The skills recheck adds four newer inspected stills and the targeted corrections in section 16. No art or implementation was produced. Current unrelated dirty/deleted/untracked work was preserved.

Material unknowns: the selected direction; actual generation acceptance rate and exposed built-in backend; arbitrary-size 2.6 documentation parity; physical target-device density/performance; live feel/audio/haptics; and human recognition/preference. The plan does not claim physical Android quality from PNGs. Public model documentation and the local skill are partially out of sync; do not resolve that by changing accounts, keys, permissions or installed tooling.

**First small next step:** spend up to two minutes reviewing direction A and the root diagnosis, and confirm or correct its central promise: **“a cared-for neighbourhood workshop, painted 2D, warm real materials, small cyan lights.”** Then request only the original root/House/symbol style exploration. That is a small, concrete production brief; it is not permission for all waves, mass replacement or APK work.

### Source identity and methodology notes

The research used the existing checkout, not an old source copy. At start, STATUS SHA256 was `A42D4E12541DE1B6E9885079CFA8350953A9B8F8B3B2F9DBDD130996EBAE34F5`; GAME_RULES was `E3C100F1F4D276A94CC1DFF9D6DF1205075C46DFEBD7FC5E69D66DFE894EFFAF`. The research STATUS handoff will intentionally change STATUS's hash.

| Owning file | SHA256 sampled during research |
| --- | --- |
| `Unity/Assets/Scripts/Content/ChapterData.cs` | `31A9178D7C8B7F719F31593B71FCD4E38A71D7DCD9B77AC2899B45E1FE32B953` |
| `Unity/Assets/Scripts/Content/ChapterRules.cs` | `1265EB52DE149AE8C794C959CC1663A1A114B77994159C8BA9740A570B6E651E` |
| `Unity/Assets/Scripts/Runtime/JunkGame.Yard.cs` | `4CE1F97E0068E652C69D132759D73B57BE83DD3BE4E61E7CDDC62DAC8819163A` |
| `Unity/Assets/Scripts/Runtime/JtnYardGraphic.cs` | `5D7A3F5718656547B9DAFC7EC92BB964AC26D92057A4DB8C97206DA561FC34DA` |
| `Unity/Assets/Scripts/Core/ExpansionRules.cs` | `43E2243429BC4FC7EA8A4452C21F18A71B3CE0521456B4EAEA85490F726CD19E` |
| `Unity/Assets/Scripts/Core/ArcadeRules.cs` | `310C87E2D0928AA90171AA39E6D1A52C5CAD169CD89ACBBECF026C6256593D65` |
| `Unity/Assets/Scripts/Runtime/JunkGame.ProductUi.cs` | `83DD42C7F1535D5BFCC6D27174428FD3747A73E7F93B801DFA295FD7170A6035` |
| `Unity/Assets/Scripts/Runtime/JunkGame.Expansion.cs` | `7BD8E5E1C4909613C6FF1876BE9BB6FBF5CCFC980C81731774CE98C72AD7DC97` |
| `Unity/Assets/Scenes/JunkToNeon.unity` | `C288F59581CF2786CFC34E2465C8D479149C6F2C6C573D30EF29C023CEB01294` |

Skills/reference material read: Unity `ui`, `ui-ugui`; `craft-game-floors` with `art-direction`, `game-feel`, `visual-scorecard`; `imagegen` with `prompting` and the game/edit portions of `sample-prompts`; `manage-sprite-atlas` for the genuine packing dependency. Craft's generic live-service, 3D lighting and default live verification steps were not applied to this research-only Canvas task. Product Design audit/ideate workflows were unnecessary; no generator-driven variants were launched. Sprite authoring, TMP optimisation and URP skills were not required for the unchanged source; their actual implementation dependencies should be re-evaluated in a later authorized wave.

Primary-source checks covered the linked OpenAI guide, model page, generation/edit references, pricing and transparency cookbook, Unity 6000.3 sprite/atlas references, uGUI 2.0 CanvasScaler and Android target/contrast guidance. Public images were viewed in a temporary background browser tab because text extraction could not establish their visual composition. No reference/game assets were downloaded or imported. Search results supplied discovery and dated Reddit entries; direct discussions and owning documentation supplied the substantive evidence. No social messages, publication, purchases, accounts or API keys were changed.

## 16. Recheck against the new Emil skills

Recheck date: **14 November 2026**. Current checkout/source baseline: **7f2c2376945006e90a175767f555508097dd51a2**. The original source-identity table above deliberately retains the initial research snapshot. This pass reviews the plan; it is not another runtime implementation or a motion-code acceptance review.

### Scope and provenance

The new package is installed **project-locally**, not newly installed into the global roots inspected in this session. [EMIL-SOURCE](../../.agents/skills/EMIL-SOURCE.md) pins it to `emilkowalski/skills` revision `0f2e23b32bb1265e84cff8d3e75785fec953fb1b`, records 19 skills and preserves the existing global copies and adapted `improve-animations` workflow. No installation, replacement or global preference update was performed in this recheck.

Applied [emil-design-eng](../../.agents/skills/emil-design-eng/SKILL.md), [apple-design](../../.agents/skills/apple-design/SKILL.md), [remove-animations](../../.agents/skills/remove-animations/SKILL.md) and [gestures](../../.agents/skills/gestures/SKILL.md) as scoped planning criteria. The user requested rechecking, so an execution-oriented skill does not authorize deleting existing motion or changing input. `learn-taste` creates rules from actual user corrections; this request supplies no new confirmed taste rule, so it does not justify a new DESIGN.md, memory entry or global rule. The inspected mobile-web, Expo, Swift and Sonner-specific workflows do not apply to the active uGUI implementation. A motion-code reviewer is not evidence that unexecuted concept timings have passed live acceptance.

The current [Emil article](https://emilkowal.ski/ui/you-dont-need-animations) argues for purpose, frequency-aware restraint and prompt feedback. [Apple's WWDC18 talk](https://developer.apple.com/videos/play/wwdc2018/803/) independently supports reducing input latency, continuous feedback and interruption. These are transferable principles; they do not establish the right art palette, renderer, Android target size or game reward cadence. The timing values used above come from the installed skill's component budgets, not an invented cross-platform performance standard.

### Findings and applied plan corrections

| Before | After | Why |
| --- | --- | --- |
| A general 160–220ms ordinary-panel transition recommendation could be applied to every tab/inspection. | Frequent tabs and repeated previews stay immediate. Optional short transitions require a specific spatial explanation on an occasional panel. | Frequent animation can become navigation friction; a new art style does not require a new route transition. |
| “Press acknowledgement 60–100ms” could be interpreted as an acknowledgement delay or early action execution. | Visual feedback starts on the first rendered pointer-down frame; optional compression settles over 100–160ms. Commit remains on the existing validated release/Submit path. | Response and transaction authority are separate; feedback must not spend, select or claim prematurely. |
| Four to six FX and “Motion” in wave 5 could read as a required effect count with interaction deferred until late production. | Basic press/cancel/reduced behaviour belongs to wave 1. Four to six is a candidate ceiling for later review, and no additional effect is a valid outcome. | Discover interaction and cancellation problems in the vertical slice. Judge effects by their purpose, not their count. |
| A playful pigeon/symbol treatment lacked an explicit frequency limit. | Contextual, bounded accents only; no mandatory looping idle motion or bounce on routine navigation. Keep the existing game reel/result lifecycle. | Reduce distraction without applying a productivity-app ban to gameplay outcomes that carry actual information. |
| The initial diagnosis relied on Sol7/8 as its newest visuals. | Add the later aligned Energy row, subdued Orders history and separated construction paragraphs to the preservation list below. | New artwork must not reintroduce issues already corrected by MAIN. |

**Verdict:** keep direction A and the source-bound inventory; amend the interaction/motion plan before production. The corrected document is suitable for direction review. It does not approve a generated style, a runtime patch or physical-device feel. The alternative media, prompt templates, alpha/import recommendations and cost scenarios remain proposals; this pass did not regenerate their examples or remeasure API usage. The original time envelope stays provisional: first-slice interaction work moves earlier rather than adding a separate motion project.

### Skill conflicts resolved for JTN

- **Press feedback versus consent:** Apple-style “respond on down” means presentation only. `JtnPanelPointerConsent` now cancels a contact permanently after it crosses the existing EventSystem movement threshold, including out-and-back movement and sparse release; small jitter remains eligible. Preserve that current behaviour rather than copying the skill's generic “away and back” wording or a fixed web-pixel threshold. No new drag/spring interaction is required by this art brief.
- **Interruption versus Busy:** do not add an animation lock or delay. Keep existing gameplay Busy and consent boundaries; “never lock input for motion” is not permission to let an invalid transaction execute mid-spin.
- **Apple materials versus the Android game:** use grouping, weight, restraint and spatial meaning. Do not adopt glass chrome, blur shaders, system-font substitution, Swift, CSS, Vaul or React as a prerequisite. The proposed cream opaque surfaces remain valid; measured readability takes priority over material fashion.
- **Browser performance versus Unity:** transform/opacity recipes do not prove a uGUI animation is free. Decorative transforms can still dirty Canvas geometry; changing text or layout can cause rebuild work. Keep per-frame text/layout changes and broad hierarchy animation out of the slice unless the owning implementation needs them; measure actual Canvas/frame/allocation cost when motion is implemented.
- **Reduced motion versus no feedback:** preserve final state and readable status. A static or colour response can be sufficient. Do not add a new settings feature or claim automatic Android accessibility-setting integration; the existing reward-animation setting's actual scope remains authoritative until separately changed.

### Newer visual/source evidence

Inspected four additional stills; the first three belong to [visual-emil-wave verification](../evidence/unity/visual-emil-wave-20261114/verification.json), the last to [visual-emil-wave2 verification](../evidence/unity/visual-emil-wave2-20261114/verification.json). The complete relevant STATUS entries were read. Their existing test receipts are retained as prior evidence, not rerun by this recheck.

| Newer inspected frame | What it establishes visually | Preserve during art integration |
| --- | --- | --- |
| [Root, short portrait](../evidence/unity/visual-emil-wave-20261114/root-short.png) | Energy now aligns vertically with Coins/Diamonds; the mixed pixel/vector/geometric art seam is still present. | Shared balance-row edges, existing action/yard clearances. The art-direction diagnosis survives this layout improvement. |
| [Orders, short portrait](../evidence/unity/visual-emil-wave-20261114/orders-short.png) | Previous reward collected recedes below Available/accept. | Historical payment must remain distinct from current availability; do not celebrate a past credit as a new payout. |
| [Settings, short portrait](../evidence/unity/visual-emil-wave-20261114/settings-short.png) | Grouped persistence wording and the aligned balance row remain legible. | Keep real caption grouping and the current setting semantics; decoration must not increase text wrapping. |
| [Workshop detail, short portrait](../evidence/unity/visual-emil-wave2-20261114/final-workshop.png) | Stage/price/action, benefit and building purpose now occupy separate paragraphs. | Preserve all three information groups and the explicit prerequisites. A family painting cannot replace the text contract. |

Since the initial nine-file identity snapshot, eight of those files remain exact; `JunkGame.ProductUi.cs` changed for the Energy alignment. Current hashes relevant to this recheck are:

| Rechecked file | SHA256 at 7f2c237 |
| --- | --- |
| `Unity/Assets/Scripts/Runtime/JunkGame.ProductUi.cs` | `0CF968DA96E5CD94B63838187CF4DD832407FFE1FF00CD1E210A75AA5879BB09` |
| `Unity/Assets/Scripts/Runtime/JtnPanelPointerConsent.cs` | `CE1A4295D59496632DD5835709C89A7442439D7AD8081A79374D25EF35CC8E40` |
| `Unity/Assets/Scripts/Runtime/JunkGame.Navigation.cs` | `216F4CE5FA1DB2B632DB2E8957D628BB09FEB2D01CCF212AEBB9E16509657647` |
| `Unity/Assets/Scripts/Runtime/JunkGame.Chapter.cs` | `0CFF0D6BB5801C45AC04495159A9DBB230907CFBF342E20CADFB31078C87E092` |

No owning construction/catalog/yard projection change was found; 30 visual states and 24 paid constructions remain the inventory basis. The latest building-detail receipt explicitly retains a failed manual era-boundary test with missing short-profile/longest-family preparation; it is not counted as passing. Its portable completed-benefits test is separate evidence. This research does not rerun or broaden that acceptance.

### First-slice observation sheet

For each proposed motion, record purpose, expected encounter frequency, trigger, affected visual only, reduced alternative and cancellation/cleanup. If it has no useful answer, omit it. During later authorized integration, observe these cases with advancing frames and private saves:

1. Down on an enabled control visibly acknowledges contact; it makes no purchase/claim/project change by itself. Release invokes at most the existing one action, without an animation wait.
2. Jitter permits a normal tap. A threshold-crossing swipe, out-and-back motion, context replacement or pause/disable cancels the contact and leaves no stuck pressed appearance. A stale release cannot activate a replacement panel.
3. Rapidly open/close or change routes; retained presentation starts from the current visible state, does not jump from a target value and introduces no delayed duplicate callback. Repeated navigation is readable immediately.
4. With reward effects disabled, final ownership, amounts and selected/saved/Ready state stay understandable. Inspection, replay and Finish create no fresh credit celebration. Ordinary authored spin/reveal timing remains exact.

This is the smallest connected interaction slice to validate alongside the first new art. Existing unchanged matching cases may be reused under the project's evidence rules; this sheet is not a request for a new broad testing campaign, device installation, assets or APK.
