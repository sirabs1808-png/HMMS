# Stockpile System

Stockpile System is an early Windows desktop prototype for a customizable inventory management system.

## Run from the Desktop

After publishing on Windows, use the `Stockpile System` shortcut on your Desktop.

## Run from source

Requires the .NET 10 SDK on Windows:
```powershell
dotnet run --project .\StockPilot.csproj
```

To publish a standalone Windows 64-bit build:
```powershell
dotnet publish .\StockPilot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

## Prototype features

- Dark, compact dashboard with top navigation, inventory KPIs, stock-level chart, low-stock alerts, and recent activity
- Product catalog with search, photos, and sample products
- Product search, add, and edit forms with local persistence
- Remove selected products with confirmation while retaining their stock activity history
- Configurable per-product reorder levels; editing product details does not change stock quantity
- Stock receiving, usage/removal, and count adjustments with a required reason
- Persistent stock activity history and low-stock/dashboard refresh
- Dashboard activity chart and recent activity derived from recorded changes
- Local supplier directory
- Category-level inventory report with CSV export
- Persistent light/dark theme and accent color presets/custom hex color
- Offline help guide for common inventory tasks

Product photos (PNG, JPEG, BMP, GIF, or TIFF, up to 10 MB each) are copied into `%LOCALAPPDATA%\StockPilot\product-images` and referenced by the local inventory file. This keeps them available offline even if the original photo is moved.

The dashboard uses local inventory and stock-movement records. Sales, work orders, and purchase-order metrics from the visual design reference are not displayed because those workflows are not implemented yet.

The inventory, activity history, and suppliers are saved together as JSON in `%LOCALAPPDATA%\StockPilot\inventory.json`. Existing `%LOCALAPPDATA%\StockPilot\products.json` data is migrated on first launch after this update. The original file is left in place. Appearance preferences are saved in `%LOCALAPPDATA%\StockPilot\appearance.json`. A fresh install is seeded with a small sample catalog. This is local persistence, not a cloud backup.

Real user accounts and roles, multi-computer synchronization, cloud backups, and the online AI assistant are planned but not implemented yet. The header labels the current profile as local; it is not a login. The offline help guide is scripted guidance, not AI. Sales and monthly gross-profit reports are planned next and will require recording actual sales rather than inferring sales from stock reductions.
