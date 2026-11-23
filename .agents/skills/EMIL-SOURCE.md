# Emil Kowalski skills in Junk to Neon

Installed on 2026-11-14 from [emilkowalski/skills](https://github.com/emilkowalski/skills/tree/0f2e23b32bb1265e84cff8d3e75785fec953fb1b/skills), pinned to `0f2e23b32bb1265e84cff8d3e75785fec953fb1b`. The GitHub directory inventory contains 19 skills. The upstream MIT notice is preserved in [EMIL-LICENSE](EMIL-LICENSE).

The project-local copies include their reference files. Global skills were preserved. `gestures` already matched the installed global copy. `improve-animations` retains the existing local adaptation of SKILL.md, AUDIT.md and PLAN-TEMPLATE.md: it distinguishes audit from authorized execution and respects the project's delegation boundary. Its upstream original is retained privately with this task's diagnostic material. This adapter is intentional; replacing it from the older upstream catalogue would lose a verified workflow correction.

## Applicability to the Unity prototype

The active UI is Unity uGUI. Transfer interaction and motion principles to that framework; framework-specific examples remain references for their own stacks. This installation does not change the preserved Phaser baseline or add React, Expo, Swift, Vaul, Sonner or browser libraries to Unity.

| Skill | Application in this project |
| --- | --- |
| [gestures](gestures/SKILL.md) | Applied to tap-versus-swipe intent in shared action buttons. A contact crossing the EventSystem movement threshold loses tap consent, including out-and-back movement and sparse release. Jitter remains a tap. No draggable sheet currently requires spring physics. |
| [emil-design-eng](emil-design-eng/SKILL.md) | Use clear feedback, touch targets, readable type and restrained hierarchy when editing uGUI. This pass preserves existing geometry and labels. |
| [animate](animate/SKILL.md) | Apply purpose, frequency, response and interruption criteria to Unity motion. No additional transition was justified by the gesture fix. |
| [animation-performance](animation-performance/SKILL.md) | Pointer movement only evaluates the owning contact; it introduces no Update polling, layout animation, Canvas rebuild or model/save write. Browser compositor prescriptions are not Unity guarantees. |
| [animation-vocabulary](animation-vocabulary/SKILL.md) | Shared terminology for reviewing motion; not a request to add effects. |
| [apple-design](apple-design/SKILL.md) | Transfer legibility, spatial clarity and restraint where appropriate; retain the Android portrait game identity. |
| [break-ui](break-ui/SKILL.md) | Exercise adverse input: long swipe, reversal, sparse release, jitter, stale contacts, multiple IDs and pause/resume. Existing assertions remain intact. |
| [choreograph](choreograph/SKILL.md) | Use only when multiple purposeful effects need sequencing. This pass adds none. |
| [find-animation-opportunities](find-animation-opportunities/SKILL.md) | Conditional audit tool. Current high-frequency controls do not need decorative entrances to satisfy the catalogue. |
| [improve-animations](improve-animations/SKILL.md) | Preserve the adapted audit/execute workflow; apply scoped findings directly when a fix is authorized. |
| [remove-animations](remove-animations/SKILL.md) | Preserve immediate navigation and avoid introducing a delayed button action. Existing reward presentation is outside this input fix. |
| [review-animations](review-animations/SKILL.md) | Use its purpose, interruption, performance and accessibility criteria for changed motion. Scoped assessment below; no new timing values require tuning. |
| [learn-taste](learn-taste/SKILL.md) | No new taste correction was supplied in this task. No preferences were invented and no global memory or DESIGN.md was changed. |
| [mobile-native](mobile-native/SKILL.md) | Reference for mobile interaction expectations. Unity remains the active implementation; physical-phone verification is a separate acceptance step. |
| [pick-ui-library](pick-ui-library/SKILL.md) | Conditional on an actual framework/library decision. The existing uGUI stack covers this repair. |
| [prototype](prototype/SKILL.md) | Available for a separately requested prototype exploration; this task preserves the current game. |
| [ask-sonner](ask-sonner/SKILL.md) | Sonner-specific source guidance; no Sonner implementation exists in the Unity target. |
| [animate-expo](animate-expo/SKILL.md) | Expo-specific authoring, available for an Expo target; not executable against uGUI. |
| [write-swift](write-swift/SKILL.md) | Swift-specific authoring, available for a Swift target; not executable against C# uGUI. |

## Scoped interaction and motion assessment

| Before | After | Why |
| --- | --- | --- |
| A long contact movement entirely within a shared button could still select a project. | Crossing the existing EventSystem threshold permanently cancels that contact's tap; release independently checks displacement. | A swipe must not masquerade as deliberate action consent. |
| Existing immediate shared-button callbacks and keyboard Submit path. | Preserved; no transition, spring, layout motion or delayed callback added. | Frequent actions need prompt response. Gesture guidance does not justify decorative movement. |

Decision: **Approve the scoped input change**. Contact identity and the existing release-token lifetime remain intact; movement cancellation is sticky per pointer, disable/context invalidation clears contacts, and valid fresh Submit remains covered. Prices, payouts, quote validity, persistence and replay rules are unchanged. No new moving effect is introduced, so this patch has no new reduced-motion branch or easing budget. This is not an exhaustive approval of all existing game motion or physical-device feel.

Verification receipts are in [the task evidence](../../docs/evidence/unity/emil-skills-20261114/verification.json). The initial failing regression is retained alongside the passing result.
