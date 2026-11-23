# Canonical codec validation performance

Measured on 2026-10-29 in the existing Unity6000.3.21f1 Editor, private X: files and fixed snapshots. The r10 economy/runtime pin precedes later construction-price, bounded-reader and functional-copy integrations. This is a Windows Editor/Mono observation, not an Android benchmark.

Each of five cases has one retained warmup and six measured physical GameSession commands, using the same script/fixtures before and after. Early state is actual fresh37 + seed1 pair8 - repair40 =5; late state is an explicitly funded fixture with24 constructions/six skills, a retained large spin receipt and an explicitly prepared ready order. Root/backup sizes vary from about2.8KiB to21.7KiB for the pending snapshot. Neither normal progress nor global settings/processes were changed.

| Command | Before median ms | After median ms | Change |
| --- | ---: | ---: | ---: |
| Early spin |20.37|22.07|+8.4%|
| Early selection |22.02|17.42|-20.9%|
| Late spin |119.43|89.05|-25.4%|
| Late selection |121.44|90.93|-25.1%|
| Late claim |94.26|73.47|-22.1%|

No early-spin gain is claimed. Small samples, filesystem/GC/Editor noise and maxima up to267ms remain; these numbers are neither a latency SLA nor a population-tail estimate. The same explicit full-validation public-method reference ran in both labels. Every actual primary and reserved-backup snapshot matched it byte-for-byte; all35 before/after primary+backup pairs also match. The reference remains the original strict pipeline, not a claimed decomposition of the optimized Store implementation.

The baseline phase measurements identify14–21ms of redundant outer validation in populated writes. Encode/Decode already validate the complete state. Two actual Flush(true)+Replace writes still cost roughly42–45ms in the populated reference; those durability operations were preserved. Reserve/apply and the strict codec retain substantial necessary cost. Only the exact JsonStateCodec type uses its existing complete validators without repeating them at the Store boundary. Decode, JSON size/depth/shape checks, generic IStateCodec input/decoded checks, two journal snapshots, locks, exception/block behavior and copies are unchanged.

Three actual stable-frame g.Spin calls per early/late actor separately include Runtime observation/render/start of presentation. Early handler51–129ms samples became32–46ms; late samples327/83/76ms became93/73/67ms. The first-use spikes and all raw intervals are retained. Median next-frame intervals moved from67.42 to54.15ms early and90.85 to86.35ms late. Presentation still lasts about3.5s. Main-thread stalls remain above a60Hz frame budget; no60fps or native-lifecycle guarantee follows. The actor waits for later stable frames before starting, avoiding the prior eval-frame anomaly.

Mono's allocation counter exists but returns0 even around a known8192-byte allocation. Its zero values are unsupported, not evidence of zero allocations, and are excluded from conclusions.

CoreChecks passes48 groups, including a new independent nonvalidating-custom-codec case: decoded coins=-1 blocks before any initial write/grant, and retry stays blocked. Canonical Encode and Decode separately reject invalid wallets. The first relocated test run failed to find immutable fixtures because the existing locator searches only cwd/parent; the corrected run uses project .work. A setup-helper namespace compile error was also rejected before execution and retained. Neither failure required a production behavior change.

Independent05 source review accepted the exact-type boundary and verified the implemented patch/test. Raw samples, helper source, hashes and scope are in `docs/evidence/unity/performance-r11/verification.json`. Final integrated EditMode, new storage-reader/price cases and focused lifecycle acceptance belong to the subsequent source batch. No new APK, device, power-loss or concurrent-adversarial-filesystem acceptance is claimed.
