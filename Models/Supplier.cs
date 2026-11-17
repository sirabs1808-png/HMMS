namespace StockPilot.Models;

public sealed class Supplier
{
    public required string Name { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}
