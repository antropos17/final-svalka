# Style C — colourful flat cartoon v003

Revised 15 November 2026 in response to the user's feedback that v002 was too dull. The new set adds brighter turquoise/aqua, sunny yellow, fresh green and small clean vermilion accents, plus clearer brickwork, window panes, fasteners and broad folds. It retains the original five-stage house progression, four symbol identities and 2D cartoon contour system.

Generated here through the built-in image generator only. Higgsfield was not used. v002 is preserved unchanged. See [the research and applied design decisions](COLOUR_RESEARCH.md) and [the full prompt/output/reference manifest](manifest.json).

## Review the artwork

- [All 53 elements together on the original navy background](review/all-assets-navy.png)
- [The same complete sheet on transparency](review/all-assets-transparent.png)
- [v002 versus v003 at the same scale and on the same background](review/v002-v003-comparison.png)

## Individual PNG exports

| Folder | Count | Contents |
|---|---:|---|
| `exports/houses/` | 5 | Ruin, structural repair, finished house, porch/fence, lived-in final state |
| `exports/symbols/` | 4 | Potato, box, tied trash bag, pigeon |
| `exports/buttons/` | 12 | Primary, secondary, neutral and round utility, each normal/pressed/disabled |
| `exports/icons/` | 32 | Navigation, currency/resource, utility and yard glyphs, ordered in `asset-order.json` |

Twelve generator originals are retained in `originals/`, and twelve exact prompts in `prompts/`. House and symbol exports are unchanged copies of full 1254x1254 canvases. Original icon atlases are 1254x1254; the button atlas is 1448x1086. Icon/round-button exports are centered at 320x320 and horizontal buttons at 640x256. The generator's actual sizes are recorded rather than inferred from prompt requests.

All 12 originals and 53 individual exports passed alpha checks. Atlas boundaries use empty alpha gutters; no meaningful glyph pixel is crossed by a cut. `alpha-qa.json` records dimensions, content bounds, transparent/opaque/edge pixel counts, cut coordinates and SHA256 hashes. Review sheets assemble the exact exports without redrawing. The complete sheet was inspected for clipping, missing assets, palette consistency and stage-specific additions.

These are art-review assets, pending the user's decision. No Unity source, scene, settings, imports, runtime labels, saves, build or device was changed. Game tests were not required for this asset-source-only revision. Runtime pivots, nine-slice borders and exact press-state alignment remain integration work. Next action: review v003's colour/detail level before integration or another art revision.
