namespace StockPilot.Models;

public sealed class CategoryStockSummary
{
    public required string Category { get; init; }
    public int ProductCount { get; init; }
    public long UnitsInStock { get; init; }
    public decimal StockValue { get; init; }
}
