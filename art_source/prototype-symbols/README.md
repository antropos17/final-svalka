# Provisional reel symbols v1

Generated on 2026-10-29 (Asia/Baku) using the built-in `image_gen.imagegen` tool with `transparent_background: true`. One new atlas generation, one selected output. No third-party artwork, external API fallback or programmatic image editing. The exact prompt is in `prompt.txt`.

Source checkout: `9e7dcaddcf746fbcf72a0715b811726c6b4a984b`, with inherited unrelated dirty files preserved. The existing generated house was visually inspected for palette/context; it was not supplied as a tool input or modified. Current rules, asset contract, art prompt and the independent asset-production brief informed this limited set. No narrative or house redesign is implied.

Original generator output: `C:/Users/murtu/.codex/generated_images/01a184cc-c840-7fb0-acbf-0942a0149d4c/exec-3ead3e73-c3dc-4435-8587-6926d3147ccf.png`. `prototype-symbols-imagegen-v1.png` and the Unity PNG are byte-identical copies with SHA-256 `0021f8ac4143ff103549459e76cd081cde9e79b59ff7f74d1b659ac0fa578acc`. The raw PNG and its provenance metadata are preserved unchanged.

## Integration

Unity texture: `Unity/Assets/Art/PrototypeSymbols/prototype-symbols-v1.png`. `manifest.json` there is authoritative for symbol IDs, sprite names and rectangles. Existing economy order remains:

| Index | ID | Sprite | Unity bottom-left rect x,y,w,h |
|---|---|---|---|
| 0 | potato | sym_potato | 0,627,627,627 |
| 1 | box | sym_box | 627,627,627,627 |
| 2 | trash | sym_trash | 0,0,627,627 |
| 3 | pigeon | sym_pigeon | 627,0,627,627 |

The requested 1024px sheet returned as 1254x1254 RGBA. Four equal 627x627 cells provide stable untrimmed canvases, centered pivots and transparent margins. Import metadata defines multiple sprites with point filtering, clamp wrapping, no mipmaps/compression/NPOT rescale, and no physics shapes. The integrator should verify these settings and four subassets in the existing Editor. No Editor/import was launched for this stream; metadata is prepared, not Unity-import verified.

Assign the four subassets to the integrator's reel Image array in economy order; preserve aspect and use one common 32px logical canvas (proposed reel display 64 logical units). Keep `raycastTarget=false` and runtime selection tied to the existing symbol index, including spin animation and restored results. The existing implementation uses TMP text; this asset commit deliberately does not replace that code or scene wiring. Keep a missing-asset fallback. No Resources move is necessary for serialized references. If procedural creation is needed, use the exact manifest rectangles with a centered pivot; do not automatically slice by opaque islands or trim each icon.

## Checks and limits

`verification.json` records read-only Pillow alpha, bounds and hash checks. All four subjects stay inside their cells; center separator row/column have maximum alpha 1. Corners are transparent. The generator view shows distinct potato, angular cardboard box, dark knotted bag and light pigeon, without text or background. Visible silhouettes occupy roughly 76–87% of their cell; margins and mass are approximate rather than pixel-perfect.

Alpha contains all 256 levels, antialiasing and faint padding residue. This is chunky pixel-inspired provisional art, not a verified integer-grid/32-colour production export. The source is intentionally preserved rather than thresholded or cropped.

`readability.html` is a compact CSS-only contact sheet referencing the unchanged source at 32px, 64px, dark-background and greyscale/blur views. Its file-URL preview was rejected by the browser URL policy; no workaround was attempted. Thus small-size rendered readability, live reel placement, Unity import and physical-device acceptance remain unverified. Next action: integrator imports the four sprites and inspects the existing portrait reels at the intended size, checking the dark bag's contrast and the pigeon feet/beak while spinning. Do not treat the unrendered contact sheet as acceptance evidence.

## Storage

Two intended PNG copies total 1,847,936 bytes; text/metadata remain small. No build, import, toolchain cache or test save was created. C: initially had about 10.0 GiB free and X: 92.2 GiB. Generated originals/provenance are retained; no disposable intermediate image batch exists and no recurring log producer was started.

Final static checks: the first plan-text extraction hit Windows stdout encoding; repeated with UTF-8 output and no image payload dump. The first staged whitespace check caught blank YAML values with trailing spaces; those were normalized before the final check. Final free space was about 10.0 GiB on C: and 92.2 GiB on X:.
