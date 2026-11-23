# Junk to Neon — Unity prototype

User-facing name: **Свалка → Неон**. Unity is the active implementation. The user selected the migration on 2026-10-29; the first Phaser prototype remains preserved at root and commit `bfd5544` for comparison.

## Open and play

Open the `Unity/` folder with installed Unity **6000.3.21f1**, then open `Assets/Scenes/JunkToNeon.unity`. The standard Unity **Device Simulator** is docked on the right, using the non-foldable Android `Punch Hole Left (1080x2340)` portrait definition. It shows the actual running scene and accepts taps in Play Mode. This design preview needs no Android OS emulator or virtualization setup. Reopen it with **Window > General > Device Simulator** if necessary.

For permanent visual edits, stop Play Mode, expand **Junk to Neon > Portrait Canvas > Safe Area** in Hierarchy, select a house/control/text object, and edit its Rect Transform, Image or text in Scene/Inspector. Save the scene with **Ctrl+S**, then press Play and select the **Simulator** tab on the right. The Canvas and control references are serialized in the scene. Layout/style changes persist; live wallet/project labels and house stage/size are still driven by game state. Changes made during Play Mode are temporary, as in a normal Unity scene.

Free three-reel spins fund one explicitly selected construction target. The chapter now spans **two eras and 24 paid constructions**, across six building families. AUTO unlocks after the first house repair and pauses when a selected construction completes; starting it again is explicit. The progress map and direct construction list distinguish completed, locked, available and selected projects; optional skills and repeatable post-chapter orders are separate from chapter completion. Current offers and retained historical target prices can differ: inspection does not reprice or purchase a saved goal. See [game rules](docs/GAME_RULES.md) for the active prices, effects and compatibility rules.

The resource shop adds Parts, Potatoes and Energy, five purchasable modules, up to three sockets, palettes, separate building equipment, potato upgrades, and an optional bonus reel with workbench/focus upgrades. Buying a module and installing it are separate explicit actions; free presets use owned modules and open sockets. Events contains Memory, Sorting and Pigeon's Parcels, earned tickets and Diamonds, three optional slot levels, resource packs and a shared next-bonus boost. Mini-game results require explicit collection, including zero-reward completion. Closing a round preserves it; abandoning it forfeits its unpaid reward without refunding the ticket. Flying reward feedback visualizes confirmed credits and does not grant another reward.

Use the bottom **District / Slot / Events / Shop / Upgrades** navigation. The header contains the character profile, settings and a separate Energy section; the main scene keeps the current goal and map/event shortcuts. The Ticket/saved-result badge also opens Events. Inspecting a building is free; choosing or building remains a separate action. The selected goal is marked in the yard. The full route list remains under **Profile → All Sections**. Graphics remain temporary; missing House textures use stage-specific procedural silhouettes.

## Verify and build

The pure C# harness is `Checks/CoreChecks.csproj` (observed with .NET 10). Unity's `JunkToNeon` EditMode filter covers the integrated behavior checks; directly affected PlayMode cases verify real-scene UI with private saves and simulated input. Follow the [warm-Editor verification loop](.agents/skills/unity-verify/SKILL.md), target this checkout's absolute `Unity/` path and preserve normal progress. Matching unchanged evidence should be reused instead of repeating campaigns. [STATUS](docs/STATUS.md) links the actual source-bound results and their limits; Editor checks do not establish Android or physical-device acceptance.

For a smaller downloadable APK, use `JunkToNeon.Editor.ProjectSetup.BuildAndroidCompact()` through the pinned Unity CLI. It writes `Unity/Builds/Android/JunkToNeon.apk` using a non-development ARM64 build, IL2CPP size optimization, Low managed stripping, release minification and lossless LZ4HC data compression. It restores the prior PlayerSettings after building. The existing `BuildAndroid()` helper still produces `JunkToNeon-debug.apk` for diagnostics. Both use the installed Unity Android tools. Check STATUS for the packaged revision and remaining device checks; a later Editor-qualified source change does not update an older APK automatically. No signing account or publication is configured; the application ID is provisional.

Progress is stored separately from the archived web version under Unity's `Application.persistentDataPath`. Original/backup snapshots are preserved when recovery is uncertain. An owned workshop earns capped offline Coin income under the saved settlement rules; Parts, Potatoes, Energy, tickets and mini-game progress do not accumulate offline. There is no automatic web-save migration, cloud service, advertising or real-money purchase. The [Save Inspector](tools/SaveInspector/README.md) explains private copied snapshots without editing live progress.

## References

- [Current status](docs/STATUS.md), [rules](docs/GAME_RULES.md), [decisions](docs/DECISIONS.md)
- [House provenance and original generation prompt](docs/ART-PROMPTS.md)
- [Historical plan review](docs/PLAN-REVIEW.md) and [unchanged original HTML](docs/reference/junk-to-neon-plan-astra%20%282%29.html)

The root npm scripts and Capacitor wrapper belong to the preserved Phaser baseline and are not the active development path.
