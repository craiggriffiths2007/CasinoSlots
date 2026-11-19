# Casino credit display

Candy Cascade now displays the same integer credit values as the MVC casino header. The internal conversion is still 100 casino credits per Candy unit; only presentation has changed.

- An account with 306,977 credits displays **306,977**, previously 3,069.77.
- A new demo's 10,000 credits display **10,000**, previously 100.00.
- Bets display **20, 50, 100, 200, 500, 1,000**. The amounts submitted to the server are unchanged.
- Balance, spin wins, cascade messages, and bonus totals use `CandyCreditDisplay.Format`.

Use **Candy Cascade > Build Web casino** for a browser build in `Builds/WebBuildCandy`. Copy the generated `Build` files to the casino's `wwwroot/Games/candy-cascade/Build` folder. Preserve the casino's existing `index.html` when updating only the display, since it contains the wallet bridge and demo routing.
