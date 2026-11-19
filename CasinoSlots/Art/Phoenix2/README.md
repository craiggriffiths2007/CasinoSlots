# Phoenix 2 jewel symbol set

Approved concept: `approved-concept.png` (2026-10-12).
Created with the built-in image-generation tool; no external stock artwork was used.

## Installed mappings

The existing filenames, enum values and Unity asset GUIDs are retained so scene references, payouts, grouped wilds and effects keep working.

| Existing sprite | New design |
| --- | --- |
| bell.png | Golden Bell |
| blueorb.png | Frost Shard |
| cherry.png | Ruby Cherries |
| diamond.png | Kite Diamond |
| greenorb.png | Emerald Leaf |
| lemon.png | Golden Lemon |
| phoenix.png | Tall Phoenix Wild |
| phoenix_2.png | Feather Crest |
| plum.png | Violet Plums |
| redorb.png | Ember Heart Wild |
| seven.png | Golden Seven |
| yelloworb.png | Sunstone |

Standard sprites: `Assets/Games/Phoenix/Sprites/`.
Grouped variants: `Assets/Resources/PhoenixArt/phoenix-double.png` and `phoenix-triple.png`.
The complete PNGs are copied with their generated alpha intact. The concept sheet is reference only and is not imported into the game.

## Generation prompts

Shared production prompt: Use case: background-extraction / production game sprite. The reference is the APPROVED Phoenix 2 concept sheet. Reproduce ONLY the specified symbol faithfully in isolation as one large clean production sprite, with genuine transparent alpha background. Preserve the reference object's design, colors, proportions and lighting. No captions, no title, no backdrop, no floor, no drop shadow, no border, no additional symbols. Do not redesign. Center the artwork with 5% transparent padding on all sides, no cropped extremities. Premium jewel-and-gold fantasy rendering.

Subject prompts:
- Golden Bell, top-left: tilted gold bell, ivory handle, feather engraving.
- Frost Shard, row 1 column 2: blue pointed faceted crystal in silver fork.
- Ruby Cherries, row 1 column 3: paired glossy red cherries, copper curved stems, green leaf.
- Kite Diamond, row 1 column 4: elongated clear diamond with gold prongs.
- Emerald Leaf, row 2 column 1: faceted green leaf jewel with gold veins. Final extraction edit: remove the diffuse gold aura and black backdrop surrounding the leaf, retaining the solid emerald leaf and thin polished gold rim/stem, with transparent alpha outside the leaf and no shadows, bloom, glow or labels.
- Golden Lemon, row 2 column 2: textured golden yellow lemon with curled dark green leaves.
- Phoenix Wild, row 2 column 3: elegant gold copper phoenix, lifted wings and flowing orange tail. Tall narrow single-reel pose, wings swept steeply upward, long flowing tail, approximately 1:2.5 width:height. Preserve bird identity. No text.
- Feather Crest, row 2 column 4: three curling orange copper gold feathers around amber teardrop. No text.
- Violet Plums, row 3 column 1: two purple plums with natural bloom, small branch and leaf. No text.
- Ember Heart Wild, row 3 column 2: faceted red heart gemstone within open gold flame claw setting, exact text WILD across heart. No floating glow, shadow, backdrop or particles.
- Golden Seven, row 3 column 3: red enamel numeral 7 with stepped gold edges and inset amber jewel. Only text is numeral 7.
- Sunstone, row 3 column 4: elongated hexagonal honey-gold cut gemstone with short bronze sunray fins. No text.
- Two-reel Phoenix: same gold copper bird, moderately spread wings and orange tail, width:height about 2:3. One bird, no text.
- Three-reel Phoenix: same gold copper bird, fully spread sweeping wings and orange tail, width:height about 1:1. One bird, no text.

## Validation

`Tools/Validation/PhoenixSymbolArtValidation.cs` provides the batch-only `PhoenixSymbolArtValidation.Run` entry point. Copy it into the Editor folder of an isolated project copy before running it. It checks all 14 sprite references and readable alpha, enters play mode and renders normal, two-reel and three-reel wild boards. Outputs go to ignored `Validation/Phoenix2Art/`; it exits Unity without saving scene changes. It does not publish or overwrite a hosted build.

## Firebird branding
The game is now displayed as Firebird. Casino card: firebird-card.png, copied to the casino's wwwroot/images/firebird.png. Generated with the built-in image tool using the approved concept as reference: landscape 3:2 card, large gold-copper phoenix, dark midnight blue/teal background, jewel accents, large gold FIREBIRD title and Ember Heart WILD. Existing routes, saved preferences and financial identifiers are retained for compatibility.


Sprite display now uses cached alpha-trimmed rectangles (two-pixel edge padding) through SymbolLibrary.TrimSprite, including grouped Firebirds. Source PNGs retain their original pixels; the reel layout fits the cropped visible bounds.

