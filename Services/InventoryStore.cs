using System.IO;
using System.Text.Json;
using StockPilot.Models;

namespace StockPilot.Services;

public sealed class InventoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true
    };

    public InventoryStore()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string directory = Path.Combine(appDataPath, "StockPilot");
        FilePath = Path.Combine(directory, "inventory.json");
        LegacyProductsPath = Path.Combine(directory, "products.json");
    }

    public string FilePath { get; }
    private string LegacyProductsPath { get; }

    public InventoryData LoadOrCreate(IEnumerable<Product> initialProducts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        if (File.Exists(FilePath))
        {
            using FileStream stream = File.OpenRead(FilePath);
            InventoryData inventory = JsonSerializer.Deserialize<InventoryData>(stream, JsonOptions)
                ?? throw new InvalidDataException("The inventory file does not contain valid data.");
            Validate(inventory);
            return inventory;
        }

        List<Product> products;
        if (File.Exists(LegacyProductsPath))
        {
            using FileStream stream = File.OpenRead(LegacyProductsPath);
            products = JsonSerializer.Deserialize<List<Product>>(stream, JsonOptions)
                ?? throw new InvalidDataException("The existing product file does not contain valid data.");
        }
        else
        {
            products = initialProducts.ToList();
        }

        InventoryData migratedInventory = new()
        {
            Products = products,
            Movements = [],
            Suppliers = []
        };
        Save(migratedInventory);
        return migratedInventory;
    }

    public void Save(InventoryData inventory)
    {
        Validate(inventory);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        string temporaryPath = $"{FilePath}.tmp";
        using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, inventory, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    private static void Validate(InventoryData inventory)
    {
        if (inventory.Products is null || inventory.Movements is null || inventory.Suppliers is null)
        {
            throw new InvalidDataException("The inventory file is missing a required list.");
        }

        HashSet<string> skus = new(StringComparer.OrdinalIgnoreCase);
        foreach (Product product in inventory.Products)
        {
            if (string.IsNullOrWhiteSpace(product.Name)
                || string.IsNullOrWhiteSpace(product.Sku)
                || string.IsNullOrWhiteSpace(product.Category)
                || product.Quantity < 0
                || product.ReorderLevel < 0
                || product.Cost < 0
                || !skus.Add(product.Sku))
            {
                throw new InvalidDataException("The inventory file contains invalid or duplicate product data.");
            }
        }

        foreach (StockMovement movement in inventory.Movements)
        {
            if (string.IsNullOrWhiteSpace(movement.ProductName)
                || string.IsNullOrWhiteSpace(movement.ProductSku)
                || string.IsNullOrWhiteSpace(movement.Action)
                || string.IsNullOrWhiteSpace(movement.Reason)
                || movement.QuantityAfter < 0
                || movement.OccurredAt == default)
            {
                throw new InvalidDataException("The inventory file contains an invalid stock activity record.");
            }
        }

        HashSet<string> supplierNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (Supplier supplier in inventory.Suppliers)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name) || !supplierNames.Add(supplier.Name))
            {
                throw new InvalidDataException("The inventory file contains an empty or duplicate supplier name.");
            }
        }
    }
}
