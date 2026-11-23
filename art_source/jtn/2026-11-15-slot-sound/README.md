# Slot sound effects

Original sounds synthesized for Junk to Neon. `synthesize.py` generates mono 48 kHz, 16-bit PCM WAVs in `.work/slot-large-win-audio-20261115/exports`. Unity imports the copies under `Unity/Assets/Resources/Audio/Slot/` as PCM with decompression on load.

- `reel-roll.wav` uses a light filtered ratchet over a quiet motor tone. Unity loops it with a pitch and gain ramp.
- `reel-seat.wav` layers a short mechanical snap and a wooden body. Unity varies its pitch and stereo position with the reel.
- `coin-credit.wav` is a 52 ms brass contact. The slot plays it softly at each visible Coin landing, with a slight rise in pitch across the stream.
- `large-win-impact.wav` is a 224 ms falling thump with brief brass overtones. It plays once when a cleared large Coin burst appears.
