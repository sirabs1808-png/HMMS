namespace StockPilot.Models;

public sealed class ActivityDay
{
    public required string DayLabel { get; init; }
    public double ReceivedHeight { get; init; }
    public double UsedHeight { get; init; }
}
