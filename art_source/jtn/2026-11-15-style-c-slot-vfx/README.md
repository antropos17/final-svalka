# Style C slot reward effects

Two separate transparent images generated with the built-in imagegen tool on 2026-11-15. The Style C Events icon and Coin icon served as style references. `originals/` preserves the original RGBA outputs. `prompts/` contains the exact prompts and `manifest.json` maps original and runtime bytes by SHA-256.

- `gold-spark.png` is a four-pointed gold glint for the particle pool.
- `gold-burst.png` is an expanding ring of short gold rays. Its transparent center keeps the settled reel symbol visible.

The exports use alpha-bounds cropping, 12 source pixels of padding and Lanczos downsampling. Color and alpha are carried through from the generated images. In Unity, the 93×128 and 256×242 exports are Sprite/Single with transparency, mipmaps, trilinear filtering and no NPOT rescaling. The renderer uses the existing Style C Coin alongside the two new sprites.

References are `Unity/Assets/Resources/Art/StyleC/Icons/events.png` and `Unity/Assets/Resources/Art/StyleC/Icons/coin.png`. Runtime copies live under `Unity/Assets/Resources/Art/StyleC/Effects/`.
