# Sweets, sound and larger board

- Board spacing increased from 82 to 90 units; its visible area is now 630 × 630 rather than 574 × 574. Symbol images increased from 68 to 84 units, with aspect ratios preserved.
- Seven full-colour sprites are loaded from `Assets/Resources/Art/SweetSymbols.png`. Tight atlas regions are defined in `CandySymbols.cs`; an importer preserves transparency and proportions. Colours are baked into the artwork and are not tinted again by the UI.
- Six original layered stereo effects live in `Assets/Resources/Audio`: spin whoosh, candy landing, cascading win, round win, bonus fanfare and button click. `Tools/make_candy_audio.py` generates them from synthesized tones and filtered noise; no external audio samples are used.
- Effects are loaded once and played through a voice pool. Column landings pan slightly left-to-right. Win notes rise through cascades. Muting stops the sound of existing voices immediately.
- Editor/desktop preview spins run on the local practice engine; browser account play retains the existing MVC wallet connection.

## Artwork provenance

Created using the built-in image-generation tool. Selected asset: `Assets/Resources/Art/SweetSymbols.png` (1774 × 887 PNG with transparency). Source artwork is preserved; Unity sprite rectangles select the individual sweets.

## Final selected generation prompt

Use case: stylized-concept. Asset type: production-ready transparent sprite atlas for an original candy slot game. Generate a 2048 by 1024 PNG with genuine transparent background. EXACT layout: 4 columns by 2 rows of equal square cells. One isolated candy centered in each cell, occupying 75 percent of cell width and height, with generous transparent padding, no objects crossing between cells. Top row left to right: 1 pink translucent strawberry gummy bear with cute molded face and rounded ears, 2 glossy mint-green jelly bean with curved indentation, 3 purple grape-flavoured wrapped hard sweet with shiny twisted wrapper ends, 4 orange fruit jelly slice with sugar crystals and citrus segments. Bottom row left to right: 5 shiny sky-blue round striped boiled sweet, 6 golden-yellow lemon bonbon with light powdered sugar, 7 golden star-shaped jelly candy with sparkling sugar and warm glowing edges, 8 EMPTY TRANSPARENT CELL. Each sweet looks delicious, tangible and meticulously sculpted, premium polished 3D casual game render, soft upper-left studio highlights, rounded volume, juicy saturated colours, subtle self-shading but NO ground shadows. Consistent camera nearly front-facing with tiny three-quarter depth, easy-to-recognize silhouettes at small game-icon sizes. Crisp clean edges. No text, letters, borders, panels, checkerboard, background, plates or extra candies. Keep all 7 separate and evenly aligned to this precise grid.
