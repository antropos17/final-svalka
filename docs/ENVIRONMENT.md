# Observed development environment

The inventory below describes the preserved Phaser milestone. Unity became authoritative later on the same date; see STATUS for the current Editor, preview and Android build result. The Capacitor JDK 21 blocker does not apply to Unity's embedded Android tools.

Inventory on 2026-10-29 (Windows, PowerShell 7.6). No global settings or other projects were modified.

- Node 24.11.1; npm 11.6.2; Git executable `X:\Programs\Git\cmd\git.exe`. This workspace initially had no Git repository or package manifest.
- PATH Java resolves to Oracle Java 8. Unity's installed Android tools at `X:\Programs\Unity\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer` include Temurin JDK 17.0.18, SDK platforms 34/35/36, build-tools 36.0.0 and adb. The other installed Unity editor also has JDK 17. No standalone Android Studio or JDK 21 was found in inspected standard/local tool locations; this is a bounded inventory, not a whole-disk absence proof.
- `adb devices` returned an empty attached-device list. The read-only command started the standard adb server. No device installation or emulator was attempted.
- Capacitor Android 8.5.3 source requires `JavaVersion.VERSION_21`. Native preflight with installed JDK 17 fails before Gradle starts. No unsupported downgrade, JDK download, SDK update or native build is claimed.
- Phaser 4.2.1, Capacitor core/Android/CLI 8.5.3, app/preferences 8.0.1, Vite 8.3.5, TypeScript 7.0.2, Vitest 5.0.5 and Playwright 1.64.1 are pinned in package-lock.json. Playwright used installed Chrome; no browser download was needed.

## Relevant plan tools actually used

- `imagegen` skill and built-in image generation: one house sheet; unchanged generated source and exact prompt are saved locally. All visuals are provisional test graphics. See ART-PROMPTS.md.
- Official Phaser npm package skills at pinned 4.2.1: `loading-assets`, `sprites-and-images`, `scale-and-responsive`. Loader start outside preload, atlas frame selection, origins, sized parent and RESIZE behavior were checked against those bundled instructions and installed types. Official [Phaser installation](https://docs.phaser.io/phaser/getting-started/installation), [Vite](https://vite.dev/guide/) and [Capacitor environment](https://capacitorjs.com/docs/getting-started/environment-setup) documentation were consulted.
- Vitest and Playwright ran directly. No `playwright-interactive` skill was installed in the available local skill directories; ordinary Playwright was sufficient for these automated scenarios. No integration or replacement skill was installed.
- `frontend-design` was supplementary guidance for the provisional HUD. It did not replace Phaser instructions or asset validation. Aseprite cleanup, PixelLab and Retro Diffusion were not used and are unnecessary for this test prototype. No custom asset-pipeline skill or catalog was created.

## Storage and temporary output

Before dependencies: C: free 11.40 GiB; X: free 100.63 GiB. After web checks: C: approximately 10.76 GiB; X: approximately 100.13 GiB. Project-local `.work/npm-cache` measured 246.4 MiB and `.work/tmp` 2.3 MiB. No Docker/WSL startup or heavy native build occurred. No unbounded diagnostic producer was observed.

Disposable retention: `.work/tmp` and test-results may be replaced after each completed run; retain final screenshots/verification receipts under docs/evidence. Inspect `.work` before native builds; the script stops at 4 GiB or below 5 GiB free. Caches can be removed after seven idle days only after confirming no associated process uses them. This age policy is manual, not an installed cleanup service. Never clear system Temp, application data, saves, SDKs or unrelated caches. Native Gradle/npm output and TEMP/TMP are redirected to X: by the script; browser checks use process TEMP/TMP when supplied.

## Unity migration inventory

The active game uses Unity6000.3.21f1 in Unity/. Personal licensing was observed active; no login, account or terms changes were made. A project-local copy of the existing Unity CLI was updated from1.0.0-beta.12 to1.0.0-beta.14; installed global executables were preserved. Pipeline0.9.1-exp.1 was added through its supported CLI to this project only. The standard2D template supplied uGUI/TMP, Input System and Test Framework; no monetization package was added.

The actual live Editor was targeted by exact project path. Scene and settings were created through its supported Pipeline script API. TMP Essentials were imported noninteractively. A real Game View was docked beside the right Inspector after restoring the maximized Scene View; the captured game screen and frame-counter/UI observations are listed in STATUS. Editor-only background overrides keep that preview ticking without enabling background processing in builds.

Android inspection independently resolved Unity's embedded OpenJDK17.0.18, SDK platforms34/35/36, build-tools36.0.0 and NDK. Active packaging uses IL2CPP/ARM64, minAPI25 (required by this Editor) and targetAPI36. Native Gradle cache is directed to .work/gradle; TEMP/TMP were set to .work/tmp before the visible Editor launch, and its log path explicitly points to .work/unity-logs/Editor.log. Project creation's supported CLI used a temporary C: log; its observed size was small and it was preserved. Inspect actual growth again after packaging rather than assume every child process follows those variables.

Final Unity observation: after import/build, C:10.55GiB free and X:94.01GiB free. Explicit Editor log1.99MiB; the largest individual AssetImportWorker logs were about0.61MiB. Gradle output was confirmed under .work/gradle (481MiB during packaging), with its own daemon log inside that directory. No unbounded diagnostic growth was observed. Keep closed diagnostic logs for at most7 idle days or64MiB per file, inspect and preserve verification receipts before removal; skip locked files and reparse points. This is a manual retention policy, not an installed rotation guarantee.

## Codex desktop MCP configuration

User-authorized server `unity-svalka` uses X:\PROJECTS\FINAL-SVALKA\.tooling\unity-cli\bin\unity.exe (beta14), args `mcp --project-path X:\PROJECTS\FINAL-SVALKA\Unity --tools script.eval,script.eval_file,capture.screenshot`. TEMP/TMP point to this project's .work/tmp. The stable local executable is ignored tooling, not a global CLI replacement. MCP protocol2024-11-05 initialize/tools/list/read-only eval passed; exposed names are eval, eval_file and screenshot. The unrestricted catalog contained duplicate names, hence the supported narrow filter.

Changed config: C:\Users\murtu\.codex\config.toml. Exact original backup: C:\Users\murtu\.codex\backups\unity-svalka-20261029\config.before.toml, SHA256 c9737338910c5326523cf354c1d1b9754d70c2f9cbc6a8713e17c066159fc51a. Verification preserved all original bytes and appended only the new server/variables; model, sandbox, approvals and existing Unity/Zero servers were preserved. Codex CLI sees the enabled entry; this running chat's tool catalog does not yet include it. Restart Codex desktop and reopen this chat to load the configuration. Official reference: [Codex MCP](https://developers.openai.com/codex/mcp).

After the authored-scene APK rebuild and MCP checks: C:10.65GiB and X:93.80GiB free. No additional heavy verification batch or diagnostic cleanup was needed. Existing manual diagnostic retention applies to .work/mcp-check as well.
