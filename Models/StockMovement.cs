using System.Globalization;

namespace StockPilot.Models;

public sealed class StockMovement
{
    public required string ProductName { get; init; }
    public required string ProductSku { get; init; }
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public int QuantityChange { get; init; }
    public int QuantityAfter { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    public string ChangeLabel => $"{(QuantityChange > 0 ? "+" : string.Empty)}{QuantityChange:N0} units";

    public string OccurredAtLabel => OccurredAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string ActionIcon => QuantityChange > 0 ? "+" : QuantityChange < 0 ? "-" : "=";
}
