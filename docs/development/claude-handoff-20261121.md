# Junk to Neon handoff to Claude

Prepared on 2026-11-21. The user wants Claude to inspect the project and own further development. Communication with the user is in Russian. Game-owned interface text is English.

## Start here

1. Work in `X:/PROJECTS/FINAL-SVALKA` on `codex/maintenance-integration-20261114`. The latest gameplay commit at handoff is `8c0828b83318a69b6cef3dd6b1ea7f97e166ae72`. Check the current HEAD and working tree before editing.
2. Read `AGENTS.md`, `CODING_STANDARDS.md`, the latest relevant complete entries in `docs/STATUS.md` and the affected sections of `docs/GAME_RULES.md`. Project-specific skills are under `.agents/skills/`.
3. Review the existing game and the latest clip, then give the user concrete findings and the next focused improvement. The most recent feature request is completed. Further priorities belong to the conversation with the user in Claude.

The Codex chat's directory `C:/Users/murtu/.codex/worktrees/3345/FINAL-SVALKA` is an older worktree on `codex/premium-slot-20261115`. It contains earlier dirty slot edits. The integrated game and latest media are in the X: checkout above.

## Current game

Unity is the active implementation in `X:/PROJECTS/FINAL-SVALKA/Unity`, using Unity `6000.3.21f1`. The entry scene is `Unity/Assets/Scenes/JunkToNeon.unity`. Runtime UI and presentation live in `Unity/Assets/Scripts/Runtime`, economy and persistence in `Unity/Assets/Scripts/Core`.

The current Style C slot has moving symbol strips, staggered stops, gold burst and sparks, Coin sprites travelling to the visible COINS icon, per-arrival counter increments, quiet per-arrival brass sounds and a dedicated large-win impact. The three reel-stop haptic requests and the positive-Coin confirmation use the existing Sound & Effects preference. Physical haptic feel is still pending a phone check.

| Credit tier in verified fixtures | Presentation |
| --- | --- |
| Ordinary +8 | Four coins, four sparks and counter steps 37 → 39 → 41 → 43 → 45 |
| Large +23 | Six coins, twelve sparks, wider burst and steps 37 → 41 → 45 → 49 → 53 → 57 → 60 |
| Large-win sound | One separate 224 ms low impact at the expanded golden burst |

A large cue uses the actual accepted Coin income at the existing threshold. Save and reward application complete before visual presentation. The counter animation displays that committed result. Simultaneous construction payments affect the final wallet. AUTO lets the previous Coin stream finish during the next spin while keeping that next outcome hidden until reveal. Pause, modal navigation, Reload and effects-off invalidate the relevant transient cues.

## Source entry points

| Concern | File under the repository root |
| --- | --- |
| Reel movement and stop timing | `Unity/Assets/Scripts/Runtime/JunkGame.ReelMotion.cs` |
| Burst, sparks, coin trajectories and landings | `Unity/Assets/Scripts/Runtime/JunkGame.RewardImpact.cs` |
| Receipt acceptance, reveal, cancellation and labels | `Unity/Assets/Scripts/Runtime/JunkGame.RewardFeedback.cs` |
| Slot audio sources and playback | `Unity/Assets/Scripts/Runtime/JunkGame.SlotSound.cs` |
| Android haptic bridge | `Unity/Assets/Scripts/Runtime/JunkGame.SlotHaptics.cs` |
| Visible Coin counter | `Unity/Assets/Scripts/Runtime/JunkGame.ProductUi.cs` |
| Spin presentation owner | `Unity/Assets/Scripts/Runtime/JunkGame.cs` |
| Slot presentation regressions | `Unity/Assets/Tests/PlayMode/PremiumSlotPresentationTests.cs` |
| Audio synthesis source | `art_source/jtn/2026-11-15-slot-sound/synthesize.py` |
| Large impact asset | `Unity/Assets/Resources/Audio/Slot/large-win-impact.wav` |

## Verified evidence

The latest video is `X:/PROJECTS/FINAL-SVALKA/docs/evidence/unity/slot-large-win-audio-20261115/slot-win-audio-comparison.mp4`. It contains two consecutive real Unity Device Simulator spins with their original captured stereo audio, labelled ordinary +8 and large +23. It is 12 seconds, 432×990, H.264 at 60 fps with AAC audio.

The video exists and its SHA-256 matched the saved receipt during this handoff. SHA-256 is `c60005e538f85346315ce44aa7437a7b46306f5b1da766790ba9d6a6f22f49d4`.

Read `docs/evidence/unity/slot-large-win-audio-20261115/verification.json` for the actual values and tests. The 2026-11-15 run passed 49 Core groups, 16/16 JunkToNeon EditMode checks and seven focused PlayMode cases, with zero Unity compile errors or warnings. The decoded MP4 has four quiet Coin contacts on +8 and six on +23, plus exactly one large-win impact. The impact is at about 10.139 seconds, within 11.3 ms of the burst frame.

These are the recorded 2026-11-15 results. This transfer performed read-only state checks and prepared documentation.

If another capture is requested, the existing task-local scripts are still present in `.work/slot-large-win-audio-20261115/`. In particular `run-capture.ps1`, `capture-ordinary.cs`, `capture-large.cs` and `encode-check.py` show the GPU readback and Unity AudioRenderer workflow. They use one-time output paths and private seed-1/seed-2 saves. Inspect their file-existence checks and assign fresh owned paths for a new run.

## Working copy and Editor

The checkout has existing tracked modifications and deletions plus untracked research and art. The latest gameplay source files are clean relative to the gameplay HEAD at handoff. Existing local changes include `AGENTS.md`, `CODING_STANDARDS.md`, `.gitignore`, `Unity/Assets/Resources/Editable/PrototypeFont.asset`, `Unity/ProjectSettings/ProjectSettings.asset`, the Pipeline configuration, deleted historical PNGs and untracked recovery/research/art directories. Preserve this local state while reviewing it. A fresh clone alone omits those local files and changes. Git reports no configured remote for this checkout.

On 2026-11-21, the pinned CLI reported `STATUS_NO_INSTANCES`. Process inspection showed CLI processes and no actual Unity Editor executable. Before opening the project, recheck current processes and CLI status because other work may have started since handoff.

Use `X:/PROJECTS/FINAL-SVALKA/.tooling/unity-cli/bin/unity.exe` beta.14 for this project, as specified by AGENTS. Its update notice offers 1.0.1. Version changes follow the project's existing authorization rules. The installed Editor is `X:/Programs/Unity/6000.3.21f1/Editor/Unity.exe`.

For game changes, follow `.agents/skills/unity-verify/SKILL.md`. Keep one Editor owner, use private saves for fixtures and preserve the regular non-foldable Android portrait Device Simulator, Scene/Hierarchy and separate locked Canvas Inspector. Read the warm Editor's effective X: temp path before private checks. The recorded previous handoff had a normal 207-Coin save, which is historical and must be re-read live.

## First message to Claude

Открой текущий проект Junk to Neon в X:\PROJECTS\FINAL-SVALKA. Прочитай CLAUDE.md и этот файл передачи, осмотри исходники и последний ролик, затем дай краткий обзор текущего состояния, конкретных проблем и следующего улучшения. Дальше продолжаем разработку только здесь. Сохрани мои локальные изменения и текущий прогресс игры.
