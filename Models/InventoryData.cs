namespace StockPilot.Models;

public sealed class InventoryData
{
    public required List<Product> Products { get; init; }
    public required List<StockMovement> Movements { get; init; }
    public List<Supplier> Suppliers { get; init; } = [];
}
