# Style C Unity support v005

Two support illustrations generated with the built-in Image Gen tool for the approved v004 screen and the v003 asset pack. Higgsfield was not used.

- `originals/courtyard-background.png`: opaque, empty courtyard environment; buildings and controls are separate runtime objects.
- `originals/initial-sites-atlas.png`: five initial sites in a 3 by 2 grid.
- `originals/initial-sites-clean.png`: Image Gen alpha cleanup of that atlas. Exterior haze visible in some viewers is RGB under transparent pixels; sampled outer/gutter pixels have alpha 0.
- `prompts/`: retained generation/edit directions for the background, initial-site atlas and alpha cleanup.

Runtime art lives in `Unity/Assets/Resources/Art/StyleC`: 5 full-canvas houses, 4 symbols, 32 icons and 12 button states from v003, plus this background and 5 cropped initial sites (59 PNGs). Symbols, icons and buttons are cropped to alpha greater than 8 with a three-pixel margin; houses preserve the shared v003 canvas. The sites are extracted from the first five 512-pixel cells of the cleaned atlas with the same alpha crop. No original v003 export was overwritten.

All runtime PNGs are Sprite Single, PPU 100, trilinear, uncompressed, max size 2048, NPOT None, alpha transparency and mipmaps enabled, readability disabled. Mipmaps and trilinear sampling were enabled in the follow-up polish pass to smooth downscaled sprites; original PNG bytes remain unchanged. Horizontal button sprites use borders (60,45,60,45); the UI uses sliced images with a multiplier of 3. Round sprites use no border. `manifest.json` records runtime and support-file hashes.

Only initial sites use these extra illustrations. Paid non-house buildings retain the existing stage geometry in the brighter palette. House paid stages 0 through 4 use the five v003 house sprites; the free era transition remains the existing paid-stage projection.
