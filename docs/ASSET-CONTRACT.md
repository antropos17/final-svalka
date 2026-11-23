# Representative house asset contract

One family only: stable object ID `house`, stages `0`, `1`, `2`. Save data stores this ID and stage, never filenames. Source: the original plan's section 09; stages follow the final plan review.

Unity migration: the unchanged sheet is also copied to `Unity/Assets/Resources/house-stages.png`; the runtime selects the same atlas rectangles. Unity pivots measure Y from the bottom, so the Phaser origin Y 0.8590853 maps to Unity pivot Y 0.1409147. The descriptions below document the original Phaser import. This remains test art; no additional cleanup or asset batch was performed.

## Current integration draft

- `assets/house-stages.png`: unchanged generated RGBA sheet, 1942 × 809 pixels; three houses left to right. `art_source/house-stages-imagegen-v1.png` preserves the identical source.
- `assets/house-stages.json`: Phaser JSON hash atlas, frames `house-0`, `house-1`, `house-2`. Individual rectangles avoid clipping because generation did not produce equal cells.
- `assets/manifest.json`: file paths, frame mapping, uniform scale and normalized origins. Read the manifest rather than infer frames from sheet width. With Vite `publicDir: 'assets'`, these files resolve from `/`.
- Load with `scene.load.atlas('house', '/house-stages.png', '/house-stages.json')`. Set frame from `frames[stage]`, scale `0.2`, and origin from `origins[stage]`. All stages share one world ground position. Origins intentionally differ to keep the main house footprint steady when the workshop extends to the right.
- Camera: orthographic three-quarter view, front and right walls visible. Light: upper left. Dark plum outline, rust and teal surfaces, amber windows; cyan/magenta accents on stage 2. No baked text or scene background. Shadows remain a scene concern.
- Runtime must retain the missing-asset placeholder path. Do not crop or rescale the generated sheet on disk; preserve its alpha. Nearest filtering is appropriate for the draft, but does not establish a uniform integer pixel grid.

## Production grid target and limits

The shared target remains **128 × 160 logical pixels** per house stage, bottom-center pivot `(64, 144)`, fixed main foundation, integer scaling and opaque/transparent sprite pixels. Slot symbols target **32 × 32**. UI text is drawn at runtime. Soft effects, if later added, live separately.

The built-in generator returned 1942 × 809 rather than the requested 1536 × 640 and introduced antialiased edges and variable pixel clusters. This first slice is a coherent, usable **art draft**, not a grid-clean final sprite export. Alpha ranges from 0 to 255 with intermediate values. Manual Aseprite cleanup and aligned equal canvases are still required before claiming the strict pixel standard. No Aseprite or physical-phone art acceptance was performed. Do not mass-produce this draft's irregular grid.

## Observed checks

Pillow read-only inspection confirmed dimensions, RGBA, transparent outer corners and actual alpha. Visible content at alpha >32 stays within atlas boundaries; first separator columns 645–657 and second separator columns 1236–1252 contain no pixels above that threshold. Every frame is inside the sheet. Visual inspection confirmed ruined → repaired → neon workshop progression and no text. Thin alpha=1 residue exists in padding; it is preserved. Device legibility and final game placement remain integration checks.
