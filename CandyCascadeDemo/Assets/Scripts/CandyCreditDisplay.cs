using System.Globalization;

namespace CandyCascade
{
    public static class CandyCreditDisplay
    {
        // Game math uses Candy units; the casino wallet uses 100 credits per unit.
        // Convert only the presentation, leaving wager and settlement values intact.
        public static string Format(decimal candyUnits) =>
            (candyUnits * 100m).ToString("N0", CultureInfo.GetCultureInfo("en-GB"));
    }
}
