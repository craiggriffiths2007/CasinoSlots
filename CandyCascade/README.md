# Candy Cascade

A separate, self-contained Unity 6.6 (6000.6.0f1) project. Open **this folder** in Unity Hub, open `Assets/Scenes/CandyGarden.nity`, and press Play. If the scene is missing, use **Candy Cascade > Create or open game scene**.

An original candy-themed cluster slot prototype with a 7×7 grid, animated tumbles, synthesized chimes, position multipliers up to 128×, free spins, quick mode, autoplay, adjustable bets, and an in-game rules panel. All artwork is generated locally by the game; there are no asset downloads or dependencies on the existing CasinoSlots project.

The game has an original illustrated sweet factory background, with glowing syrup tanks, candy pipes, and jellybean conveyors. The image fills the window without stretching, with soft shading and text shadows for readability. Panel fills, board frames, cell tiles, and button backgrounds remain removed. The art lives in `Assets/Resources/Art/SweetFactory.png`; its generation prompt is in `Documentation/SweetFactory-art.md`.

Each spin sweeps the old symbols down and feeds a new grid in from above. Winning symbols burst; surviving symbols fall by the exact number of empty cells underneath them, while replacements enter through the top. Drops accelerate with a small landing bounce and squash, and an invisible mask keeps symbols inside the grid. Multiplier markers remain attached to their positions. Quick mode shortens these animations.

## Playing

- Start with 1,000 fictional credits. Pick a bet, then Spin.
- Five or more equal candies connected along edges pay and tumble. Diagonals do not connect.
- In both base-game and free spins, the first win at every position creates 2× for future wins; repeat wins double it up to 128×. Existing multipliers in each winning cluster add together. Paid spins clear marks; free spins keep them.
- Three stars on the settled board award ten free spins, plus two per extra star. Three or more stars in the bonus add five spins. Stars are not cluster symbols.
- Buy Free Spins shows a confirmation for ten spins at 100× the selected bet. Confirming deducts the cost; cancelling costs nothing. Connected web purchases use the authenticated server endpoint, which settles all purchased spins and retriggers in one transaction and returns the animation receipt. Bets are locked through the bonus. Auto can be turned off while a spin is running; awarded free spins still finish automatically.
- The rules panel includes the full paytable. A spin stops at 5,000× bet or 100 tumbles. Credits are held at decimal precision and displayed to two decimals. Closing the game resets the balance.

## Build and validation

Use **Candy Cascade > Validate game rules** for deterministic engine checks and a 1,000-spin simulation. Use **Candy Cascade > Build Web casino** to validate and build the browser game in `Builds/WebBuildCandy` (index.html, Build and TemplateData). The default command-line method `CandyProject.Build` also builds Web. **Candy Cascade > Build Windows demo** and `CandyProject.BuildWindows` are optional desktop diagnostics.

Launching the Windows demo with `-candyCapture -candyMotionCapture` runs a paid-spin, deterministic cascade, and free-spin smoke test and saves animation frames in `Builds/Windows/Motion`. The window must be visible for screenshots to render.

For command-line builds:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\repos\CasinoSlots\CandyCascade' -executeMethod CandyProject.Build -logFile 'C:\repos\CasinoSlots\CandyCascade\Validation\build.log'
```

The game constructs its interface at runtime in `CandyGame.cs`; the scene contains its entry component. `CandyEngine.cs` contains Unity-independent rules and the paytable. `CandyProject.cs` supplies scene setup, validation, and build commands. To customize visuals, edit the palette and procedural shapes in `CandyGame`.

This is an original prototype inspired by candy cluster slots, not a replica or affiliated version of Sugar Rush. It does not use that game's branding, art, sounds, proprietary math, or certified payout model. No payments, real-money wagering, casino credit integration, networking, or persistent accounts are included.

## Compact casino player

The player now uses a 1240×800 frame, removes the title section, centers the board, narrows side margins and puts Spin at the far right. Space, Enter and numeric Enter spin from either the game or surrounding casino page. Held keys do not repeatedly spin. Form fields, modifiers, help and purchase dialogs, autoplay and bonus playback are guarded. Native diagnostic screenshots use offscreen rendering and can run with the window hidden.

