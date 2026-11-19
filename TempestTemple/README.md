# Tempest Temple — Unity

Standalone Unity 6000.6.0f1 project, alongside Phoenix (`CasinoSlots`) and `CandyCascade`.

## Play

Run `Builds/Windows/TempestTemple.exe`. Starts with 10,000 local practice credits. These are separate from the casino account. Use Reset Demo to refill. Music and sound preferences persist; the practice balance resets when restarting.

## Edit

Add this folder in Unity Hub and open `Assets/Scenes/TempestTemple.unity`. Press Play. The scene contains `TempestGame`, which constructs its UI at runtime, following the Candy Cascade project's approach.

The **Tempest Temple** editor menu opens the scene, validates rules, builds the Windows practice demo, or builds the WebGL casino player. The Windows build is included.

## Casino integration

The WebGL player is embedded at the existing `/Tempest` and `/Demo/Tempest` casino pages. It reads its initial balance and mode from the parent page, then calls the original `/Tempest/Spin` or `/Demo/TempestSpin` endpoint with the page's anti-forgery token. All browser outcomes and credit changes are generated and settled by the server; Unity only animates the returned round. A failed or uncertain request stops further spins until reload, with no automatic POST retry.

The browser build disables the local reset button. Demo resets use the existing server form; account credits can never be reset by the player. Windows and Unity Editor keep their local practice mode.

Build with **Tempest Temple → Build WebGL casino player**, then copy the contents of `Builds/WebGL` to `C:/repos/craiggriffiths2007/Casino/Casino/wwwroot/Games/TempestTemple`. The matching host is `Casino/Views/Tempest/Index.cshtml` plus `wwwroot/js/tempest-host.js`. Publish the casino through its usual deployment workflow. No separate account endpoints or database migrations are required.

## Cache-safe updates

Every Web build (including Unity's normal Build button) enables content-hashed
filenames for the loader, framework, data and WebAssembly assets. Changed files
therefore get new URLs. The casino versions the iframe URL from the contents of
the generated index.html and sends Cache-Control: no-cache for TempestTemple
files, allowing cached bytes to be reused only after server revalidation.

Publish the casino application changes once, then rebuild the Unity Web player.
For future releases, upload the new Build assets first and index.html last (or
deploy the complete release atomically). Retain the previous hashed assets during
the transition so players already loading the previous release can finish.
Avoid a CDN rule that overrides origin cache headers or ignores version queries;
purge any existing CDN cache once when adopting this change. An already-running
game updates on the next page load, not during a round.

## Current rules and validation

- 6 columns by 5 rows; eight or more matching regular symbols pay anywhere.
- Reduced three-tier paytable for more frequent, smaller wins and 2x–500x lightning values, with original Tempest probabilities.
- Winning free-spin lightning adds to the persistent bank; the bank applies only when new lightning lands on a winning spin.
- Four or more base scatters award 15 free spins; three or more bonus scatters add five.
- Bonus purchase costs 100x the selected bet and includes a four-scatter trigger spin followed by 15 free spins.
- Base scatter awards are 3x / 5x / 100x for 4 / 5 / 6+ scatters. Maximum round win is 5,000x bet.
- Opening boards favour one randomly selected low-value symbol; refills use ordinary weights. No fixed seven-symbol near-miss boards are forced.
- Each spin stops after at most 12 paid winning tumbles. Every displayed win is paid, including the twelfth.
- Bets are 20–1,000 credits in steps of 20, allowing exact integer-credit payouts.
- Confirmation and bonus notifications wait for player acknowledgement.

TempestMath.cs is shared verbatim by the casino server and Unity. Server outcomes use cryptographic randomness; seeded RNG is reserved for validation. The exact commercial game's reel probabilities are not public; this is not identical or certified Gates of Olympus mathematics.

Current balance: tempest-gems-v4. A 1,000,000-round sample returned 97.328% (95% interval ±2.389 percentage points), with 53.505% payout frequency and 43.038% sub-bet payouts. 200,000 bonus purchases returned 95.222% of cost (±0.523 points). These estimates are not certified RTP; older v3 results do not describe this balance.

The eleven symbols use bold generated artwork with distinct silhouettes and colours, thick outlines and transparent backgrounds. The original underwater background remains in use.

Run the Windows executable with `-tempestCapture <absolute-output-directory>` for automatic animation, bonus-dialog and settlement smoke testing with screenshots. Diagnostic mode uses seeded rounds without changing production outcomes.



## Compact layout and shortcuts

The large title is removed, the game reference resolution is 1380×800, and the
Spin button is at the far right. Space, Enter and numeric Enter trigger a
regular spin. Holding a key does not repeatedly spin or scroll the page.
Shortcuts work with the iframe or the parent casino page focused, but ignore
editable fields, links and browser buttons. The same BeginSpin guard blocks
spins during rounds, help, purchase confirmation and bonus notifications.
Keyboard shortcuts never confirm a bonus purchase. Native players use Unity
GetKeyDown; WebGL uses the template's KeyboardSpin message entry point.
Unity navigation Submit events are disabled to avoid a second button activation.

Four gems have increased weights and smaller three-tier payouts. Base scatter
and bonus lightning probabilities were rebalanced alongside the weights.
The casino and Unity TempestMath.cs files must remain identical.