using CandyCascade;
using Casino.Data;
using Casino.Models;
using Casino.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Casino.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CandyCascadeController(ApplicationDbContext db, UserManager<IdentityUser> users, IAntiforgery antiforgery) : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { IncludeFields = true };

    [HttpGet("Game/CandyCascade")]
    public IActionResult Play() => View("~/Views/Game/CandyCascade.cshtml");

    [AllowAnonymous, HttpGet("Demo/CandyCascade")]
    public IActionResult Demo()
    {
        ViewBag.IsDemo = true;
        return View("~/Views/Game/CandyCascade.cshtml");
    }

    [HttpGet("Game/CandyCascade/account")]
    public async Task<IActionResult> Account()
    {
        var userId = users.GetUserId(User); if (userId == null) return Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var player = await LockedPlayer(userId);
        if (player == null)
        {
            player = new PlayerAccount { UserId = userId, Credits = 10000, Created = DateTime.UtcNow };
            db.PlayerAccounts.Add(player); await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return Json(new { userId, balance = player.Credits, token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpPost("Game/CandyCascade/spin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Spin([FromForm] long bet, [FromForm] Guid requestId, [FromForm] string accountId)
    {
        var userId = users.GetUserId(User); if (userId == null) return Unauthorized();
        if (userId != accountId) return Conflict(new { message = "Account changed. Reload the game." });
        if (requestId == Guid.Empty || !CandyRoundService.ValidBet(bet))
            return BadRequest(new { message = "Choose 10, 25, 50 or 100 credits." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var player = await LockedPlayer(userId);
        if (player == null) return BadRequest(new { message = "Reload the game to create your player account." });
        string key = requestId.ToString("D");
        var previous = await db.Spins.FromSqlInterpolated($"""
            SELECT * FROM [Spins] WHERE [UserId] = {userId}
            AND JSON_VALUE(CASE WHEN ISJSON([Result]) = 1 THEN [Result] ELSE N'null' END, '$.game') = N'Candy Cascade'
            AND JSON_VALUE(CASE WHEN ISJSON([Result]) = 1 THEN [Result] ELSE N'null' END, '$.requestId') = {key}
            """).AsNoTracking().FirstOrDefaultAsync();
        if (previous != null)
        {
            if (previous.Bet != bet) return Conflict(new { message = "This request already used a different bet." });
            using var receipt = JsonDocument.Parse(previous.Result);
            var replay = receipt.RootElement.GetProperty("response").Deserialize<CandyReceipt>(JsonOptions)!;
            replay.balance = player.Credits;
            await transaction.CommitAsync(); return new JsonResult(replay, JsonOptions);
        }
        if (player.Credits < bet) return BadRequest(new { message = "Not enough casino credits.", balance = player.Credits });
        var response = CandyRoundService.Play(bet, key);
        player.Credits = checked(player.Credits - bet + response.win); response.balance = player.Credits;
        db.Spins.Add(new Spin {
            UserId = userId, Bet = bet, Win = response.win, Created = DateTime.UtcNow,
            Result = JsonSerializer.Serialize(new { game = "Candy Cascade", requestId = key, response }, JsonOptions)
        });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return new JsonResult(response, JsonOptions);
    }

    private Task<PlayerAccount?> LockedPlayer(string userId) => db.PlayerAccounts
        .FromSqlInterpolated($"SELECT * FROM [PlayerAccounts] WITH (UPDLOCK, HOLDLOCK) WHERE [UserId] = {userId}")
        .SingleOrDefaultAsync();
}
