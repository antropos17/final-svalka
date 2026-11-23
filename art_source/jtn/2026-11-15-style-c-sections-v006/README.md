# Style C section artwork v006

18 new transparent emblems/icons generated with the built-in imagegen tool in this chat on 2026-11-15. Higgsfield was not used. Each asset was a separate generation; its exact prompt is in `prompts/`. The preserved outputs are in `originals/`.

References: `../2026-11-15-style-c-colour-v003/review/all-assets-navy.png` and the existing `Unity/Assets/Resources/Art/StyleC/Symbols/symbol-pigeon.png` for Pigeon's Parcels. This extends the v003 family: flat cartoon shapes, dark teal outlines, turquoise, warm gold, cream, green and clear red accents. Section names remain editable TMP text; the emblems contain no baked lettering.

## Contents

- Seven section emblems: district, events, shop, upgrades, energy, profile, settings.
- Three game emblems: Memory/Yard Finds, Sorting Shift, Pigeon's Parcels.
- Eight category icons: modules, sockets/presets, equipment, skills, orders, progress, appearance, trades.

## Runtime exports

`Unity/Assets/Resources/Art/StyleC/Sections/` contains cropped PNGs with a maximum long edge of 512 pixels, preserving aspect and alpha. `tools/export.ps1`, run from the repository root in a fresh PowerShell process, performs only alpha-bound cropping with four source pixels of padding, bicubic resizing and review-sheet assembly. It does not draw or recolor the artwork. Bounds use alpha > 8 to exclude faint peripheral noise; `manifest.json` records original dimensions, bounds, nonzero source-border alpha pixels and SHA-256 hashes for originals and exports.

Unity import: Sprite/Single, 100 pixels per unit, alpha transparency, uncompressed, NPOT scaling disabled, mipmaps and Trilinear filtering. Runtime UI preserves natural aspect; decorative artwork does not receive pointer events.

`review/all-sections-navy.png` shows all 18 exports together. Live Unity screen captures and verification are in `docs/evidence/unity/style-c-sections-20261115/`.
