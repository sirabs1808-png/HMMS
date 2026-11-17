using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StockPilot.Models;
using StockPilot.Services;

namespace StockPilot;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Product> _products = new();
    private readonly ObservableCollection<StockMovement> _movements = new();
    private readonly ObservableCollection<Supplier> _suppliers = new();
    private readonly InventoryStore _inventoryStore = new();
    private readonly AppearanceSettingsStore _appearanceSettingsStore = new();
    private readonly ProductImageStore _productImageStore = new();
    private readonly ICollectionView? _productsView;
    private AppearanceSettings _appearanceSettings = new();
    private Product? _editingProduct;
    private string? _selectedProductImagePath;
    private BitmapImage? _selectedProductImage;
    private bool _removeProductImage;
    private bool _hasProductImage;
    private bool _isDarkTheme;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _appearanceSettings = _appearanceSettingsStore.Load();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                $"Stockpile System couldn't load your appearance preferences from:\n{_appearanceSettingsStore.FilePath}\n\n{exception.Message}\n\nDefault appearance settings will be used.",
                "Appearance preferences couldn't be loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _appearanceSettings = new AppearanceSettings();
        }

        _isDarkTheme = _appearanceSettings.IsDarkTheme;
        if (ColorConverter.ConvertFromString(_appearanceSettings.AccentColor) is Color savedAccent)
        {
            ApplyAccentResources(savedAccent);
        }

        ApplyThemeResources(_isDarkTheme);
        UpdateThemeToggleLabel();
        CustomAccentBox.Text = _appearanceSettings.AccentColor;

        List<Product> initialProducts =
        [
            new() { Name = "Wireless Mouse", Sku = "EL-2048", Category = "Electronics", Quantity = 26, ReorderLevel = 10, Cost = 18.50m },
            new() { Name = "Thermal Labels", Sku = "PK-1102", Category = "Packaging", Quantity = 4, ReorderLevel = 12, Cost = 8.25m },
            new() { Name = "Paper Towels", Sku = "OF-3055", Category = "Office", Quantity = 29, ReorderLevel = 15, Cost = 3.40m },
            new() { Name = "USB-C Cable", Sku = "EL-3301", Category = "Electronics", Quantity = 7, ReorderLevel = 10, Cost = 6.99m },
            new() { Name = "Storage Bin", Sku = "WH-0814", Category = "Warehouse", Quantity = 18, ReorderLevel = 8, Cost = 12.00m },
            new() { Name = "Packing Tape", Sku = "PK-0920", Category = "Packaging", Quantity = 45, ReorderLevel = 10, Cost = 2.75m }
        ];

        InventoryData inventory;
        try
        {
            inventory = _inventoryStore.LoadOrCreate(initialProducts);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                $"Stockpile System couldn't load the saved inventory from:\n{_inventoryStore.FilePath}\n\n{exception.Message}\n\nThe app will close without changing the file.",
                "Inventory couldn't be loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
            return;
        }

        foreach (Product product in inventory.Products)
        {
            try
            {
                product.Image = _productImageStore.LoadManagedImage(product.ImageFileName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
            {
                MessageBox.Show(
                    $"Stockpile System couldn't load the saved image for '{product.Name}'. The product will use a placeholder until you choose another image.\n\n{exception.Message}",
                    "Product image couldn't be loaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _products.Add(product);
        }

        foreach (StockMovement movement in inventory.Movements.OrderByDescending(item => item.OccurredAt))
        {
            _movements.Add(movement);
        }

        foreach (Supplier supplier in inventory.Suppliers.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            _suppliers.Add(supplier);
        }

        _productsView = CollectionViewSource.GetDefaultView(_products);
        _productsView.Filter = FilterProduct;
        ProductsGrid.ItemsSource = _productsView;
        ActivityGrid.ItemsSource = _movements;
        SuppliersGrid.ItemsSource = _suppliers;
        DashboardDateText.Text = DateTime.Now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture).ToUpperInvariant();
        RefreshInventorySummary();
        RefreshActivityLists();
        RefreshActivityChart();
    }

    private bool FilterProduct(object item)
    {
        if (item is not Product product)
        {
            return false;
        }

        string query = SearchBox.Text.Trim();
        return query.Length == 0
            || product.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || product.Sku.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || product.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _productsView?.Refresh();
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string destination)
        {
            return;
        }

        foreach (object child in ((Panel)DashboardNav.Parent).Children)
        {
            if (child is Button navButton)
            {
                navButton.Background = Brushes.Transparent;
                navButton.SetResourceReference(Control.ForegroundProperty, "TextMutedBrush");
            }
        }

        button.SetResourceReference(Control.BackgroundProperty, "AccentSoftBrush");
        button.SetResourceReference(Control.ForegroundProperty, "AccentBrush");

        DashboardView.Visibility = destination == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ProductsView.Visibility = destination == "Products" ? Visibility.Visible : Visibility.Collapsed;
        ActivityView.Visibility = destination == "Activity" ? Visibility.Visible : Visibility.Collapsed;
        HelpView.Visibility = destination == "Help" ? Visibility.Visible : Visibility.Collapsed;
        SuppliersView.Visibility = destination == "Suppliers" ? Visibility.Visible : Visibility.Collapsed;
        ReportsView.Visibility = destination == "Reports" ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderView.Visibility = destination is "Dashboard" or "Products" or "Activity" or "Help" or "Suppliers" or "Reports"
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (destination == "Dashboard")
        {
            ScreenTitle.Text = "Dashboard";
            ScreenSubtitle.Text = "Monitor stock levels and recent inventory activity.";
        }
        else
        {
            ScreenTitle.Text = destination;
            ScreenSubtitle.Text = destination switch
            {
                "Products" => "Browse, search, and manage your inventory.",
                "Activity" => "A clear record of every stock change.",
                "Suppliers" => "Keep your supplier contact details in one place.",
                "Reports" => "Review stock value, levels, and recent activity.",
                "Help" => "Quick answers for common inventory tasks.",
                _ => "This area is planned for a future build phase."
            };
            if (destination is not "Help" and not "Suppliers" and not "Reports")
            {
                PlaceholderTitle.Text = $"{destination} is coming soon";
            }
        }
    }

    private void ViewProducts_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Products");
    }

    private void ViewActivity_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Activity");
    }

    private void BackToDashboard_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo("Dashboard");
    }

    private void QuickGuide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string question)
        {
            ShowGuideAnswer(question);
        }
    }

    private void AskGuide_Click(object sender, RoutedEventArgs e)
    {
        string question = GuideQuestionBox.Text.Trim();
        if (question.Length == 0)
        {
            GuideQuestionText.Text = "Type a question first.";
            GuideResponseText.Text = "You can ask about adding products, receiving stock, recording usage, adjusting a count, changing colors, or low-stock alerts.";
            return;
        }

        ShowGuideAnswer(question);
        GuideQuestionBox.Clear();
    }

    private void GuideQuestion_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            AskGuide_Click(sender, new RoutedEventArgs());
        }
    }

    private void ShowGuideAnswer(string question)
    {
        string normalizedQuestion = question.ToLowerInvariant();
        string response;

        if (normalizedQuestion.Contains("add") || normalizedQuestion.Contains("new product"))
        {
            response = "Select Add item at the top, enter a product name, unique SKU, category, quantity, and unit cost, optionally choose a product photo, then save. Products are saved on this computer.";
        }
        else if (normalizedQuestion.Contains("receive"))
        {
            response = "Open Stock activity and select Record stock change. Choose a product, select Receive stock, enter the amount and a reason, then save.";
        }
        else if (normalizedQuestion.Contains("delete")
            || normalizedQuestion.Contains("remove product")
            || normalizedQuestion.Contains("remove item"))
        {
            response = "Open Inventory, select the product, and choose Remove selected. Confirm the prompt. Any remaining stock is removed from tracked inventory, but its stock activity history is kept.";
        }
        else if (normalizedQuestion.Contains("use") || normalizedQuestion.Contains("remove") || normalizedQuestion.Contains("sell"))
        {
            response = "Open Stock activity and select Record stock change. Choose Use or remove stock, enter the quantity and reason. Stockpile System won't allow removing more than is available.";
        }
        else if (normalizedQuestion.Contains("count") || normalizedQuestion.Contains("adjust") || normalizedQuestion.Contains("correct"))
        {
            response = "Open Stock activity and select Record stock change. Choose Set counted stock level, enter the quantity you counted and a reason such as Cycle count, then save.";
        }
        else if (normalizedQuestion.Contains("color") || normalizedQuestion.Contains("theme") || normalizedQuestion.Contains("dark"))
        {
            response = "Open Appearance near the bottom of the sidebar. Pick a color preset, enter a custom #RRGGBB color, or switch between light and dark themes. Your choice is saved on this computer.";
        }
        else if (normalizedQuestion.Contains("low stock") || normalizedQuestion.Contains("reorder"))
        {
            response = "Each product has a configurable reorder level. Edit a product to change it; the dashboard and reports flag items at or below that level. Change stock quantities through Stock activity so each change is recorded.";
        }
        else if (normalizedQuestion.Contains("supplier"))
        {
            response = "Open Suppliers in the sidebar and select Add supplier. Enter a name; contact, email, and phone are optional. Supplier details are saved on this computer.";
        }
        else if (normalizedQuestion.Contains("report") || normalizedQuestion.Contains("export") || normalizedQuestion.Contains("csv"))
        {
            response = "Open Reports in the sidebar to review stock value and quantities by category. Select Export inventory CSV and choose where to save the file.";
        }
        else if (normalizedQuestion.Contains("login") || normalizedQuestion.Contains("sign in") || normalizedQuestion.Contains("account"))
        {
            response = "Real sign-in and user accounts are not connected yet. The profile shown is local-only and does not protect the app with a password.";
        }
        else
        {
            response = "This offline guide has built-in tips for products, stock changes, appearance, and low-stock alerts. Try a question like “How do I receive stock?”";
        }

        GuideQuestionText.Text = question;
        GuideResponseText.Text = response;
    }

    private void NavigateTo(string destination)
    {
        Button? target = destination == "Dashboard" ? DashboardNav : FindNavButton(destination);
        if (target is not null)
        {
            Navigate_Click(target, new RoutedEventArgs());
        }
    }

    private Button? FindNavButton(string destination)
    {
        foreach (object child in ((Panel)DashboardNav.Parent).Children)
        {
            if (child is Button button && string.Equals(button.Tag as string, destination, StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        _editingProduct = null;
        _selectedProductImagePath = null;
        _selectedProductImage = null;
        _removeProductImage = false;
        _hasProductImage = false;
        UpdateProductImagePreview(null, "No product photo");
        RemoveProductImageButton.IsEnabled = false;
        ProductFormTitle.Text = "Add a product";
        ProductFormSubtitle.Text = "Add an item to your catalog.";
        QuantityLabel.Text = "Opening quantity";
        QuantityBox.IsEnabled = true;
        ReorderLevelBox.Text = "5";
        ProductFormHint.Visibility = Visibility.Collapsed;
        SaveProductButton.Content = "Save product";
        FormErrorText.Visibility = Visibility.Collapsed;
        ProductNameBox.Clear();
        SkuBox.Clear();
        CategoryBox.Clear();
        QuantityBox.Text = "0";
        CostBox.Text = "0.00";
        AddProductOverlay.Visibility = Visibility.Visible;
        ProductNameBox.Focus();
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not Product product)
        {
            return;
        }

        _editingProduct = product;
        _selectedProductImagePath = null;
        _selectedProductImage = null;
        _removeProductImage = false;
        _hasProductImage = !string.IsNullOrWhiteSpace(product.ImageFileName);
        UpdateProductImagePreview(
            product.Image,
            product.Image is null && _hasProductImage ? "Saved photo unavailable" : _hasProductImage ? "Product photo" : "No product photo");
        RemoveProductImageButton.IsEnabled = _hasProductImage;
        ProductFormTitle.Text = "Edit product";
        ProductFormSubtitle.Text = "Update product details and its reorder level.";
        QuantityLabel.Text = "Current stock";
        QuantityBox.Text = product.Quantity.ToString(CultureInfo.CurrentCulture);
        QuantityBox.IsEnabled = false;
        ReorderLevelBox.Text = product.ReorderLevel.ToString(CultureInfo.CurrentCulture);
        ProductFormHint.Visibility = Visibility.Visible;
        SaveProductButton.Content = "Save changes";
        FormErrorText.Visibility = Visibility.Collapsed;
        ProductNameBox.Text = product.Name;
        SkuBox.Text = product.Sku;
        CategoryBox.Text = product.Category;
        CostBox.Text = product.Cost.ToString("0.00", CultureInfo.CurrentCulture);
        AddProductOverlay.Visibility = Visibility.Visible;
        ProductNameBox.Focus();
    }

    private void ChooseProductImage_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Choose a product photo",
            Filter = "Product images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _selectedProductImage = _productImageStore.LoadPreviewImage(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or FormatException)
        {
            ShowFormError($"Couldn't use that image. Choose a supported image smaller than 10 MB. {exception.Message}");
            return;
        }

        _selectedProductImagePath = dialog.FileName;
        _removeProductImage = false;
        _hasProductImage = true;
        RemoveProductImageButton.IsEnabled = true;
        UpdateProductImagePreview(_selectedProductImage, $"Selected: {Path.GetFileName(dialog.FileName)}");
        FormErrorText.Visibility = Visibility.Collapsed;
    }

    private void RemoveProductImage_Click(object sender, RoutedEventArgs e)
    {
        _selectedProductImagePath = null;
        _selectedProductImage = null;
        _removeProductImage = true;
        _hasProductImage = false;
        RemoveProductImageButton.IsEnabled = false;
        UpdateProductImagePreview(null, "No product photo");
    }

    private void UpdateProductImagePreview(BitmapImage? image, string status)
    {
        ProductImagePreview.Source = image;
        ProductImagePreview.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        ProductImagePlaceholder.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        ProductImageStatusText.Text = status;
    }

    private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EditProductButton.IsEnabled = ProductsGrid.SelectedItem is Product;
        RemoveProductButton.IsEnabled = ProductsGrid.SelectedItem is Product;
    }

    private void RemoveProduct_Click(object sender, RoutedEventArgs e)
    {
        if (ProductsGrid.SelectedItem is not Product selectedProduct)
        {
            return;
        }

        int productIndex = _products.IndexOf(selectedProduct);
        if (productIndex < 0)
        {
            MessageBox.Show(
                "The selected product is no longer in the catalog. Refresh the inventory and try again.",
                "Product is no longer available",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        string stockNotice = selectedProduct.Quantity > 0
            ? $"\n\n{selectedProduct.Quantity:N0} units currently in stock will be removed from tracked inventory."
            : string.Empty;
        MessageBoxResult confirmation = MessageBox.Show(
            $"Remove '{selectedProduct.Name}' (SKU {selectedProduct.Sku}) from the product catalog?{stockNotice}\n\nIts stock activity history will be kept. Any photo used only by this product will also be removed. This cannot be undone.",
            "Remove product",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        List<Product> updatedProducts = _products.ToList();
        updatedProducts.RemoveAt(productIndex);
        try
        {
            _inventoryStore.Save(new InventoryData
            {
                Products = updatedProducts,
                Movements = _movements.ToList(),
                Suppliers = _suppliers.ToList()
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                $"Couldn't remove the product. Your saved inventory was not changed. {exception.Message}",
                "Product couldn't be removed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _products.RemoveAt(productIndex);
        ProductsGrid.SelectedItem = null;
        _productsView?.Refresh();
        RefreshInventorySummary();

        string? imageFileName = selectedProduct.ImageFileName;
        if (imageFileName is not null &&
            !_products.Any(product => string.Equals(product.ImageFileName, imageFileName, StringComparison.Ordinal)))
        {
            try
            {
                _productImageStore.DeleteImage(imageFileName);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"The product was removed, but its photo couldn't be deleted.\n\n{exception.Message}",
                    "Product photo couldn't be removed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private void CloseAddProduct_Click(object sender, RoutedEventArgs e)
    {
        AddProductOverlay.Visibility = Visibility.Collapsed;
        _editingProduct = null;
    }

    private void SaveProduct_Click(object sender, RoutedEventArgs e)
    {
        string name = ProductNameBox.Text.Trim();
        string sku = SkuBox.Text.Trim();
        string category = CategoryBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(category))
        {
            ShowFormError("Enter a product name, SKU, and category.");
            return;
        }

        if (_products.Any(product =>
                !ReferenceEquals(product, _editingProduct) &&
                string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase)))
        {
            ShowFormError("That SKU is already in the catalog. Enter a unique SKU.");
            return;
        }

        int quantity = _editingProduct?.Quantity ?? 0;
        if (_editingProduct is null &&
            (!int.TryParse(QuantityBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out quantity) || quantity < 0))
        {
            ShowFormError("Quantity must be a whole number greater than or equal to zero.");
            return;
        }

        if (!int.TryParse(ReorderLevelBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int reorderLevel) || reorderLevel < 0)
        {
            ShowFormError("Reorder level must be a whole number greater than or equal to zero.");
            return;
        }

        if (!decimal.TryParse(CostBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal cost) || cost < 0)
        {
            ShowFormError("Unit cost must be a valid amount greater than or equal to zero.");
            return;
        }

        int editingIndex = _editingProduct is null ? -1 : _products.IndexOf(_editingProduct);
        if (_editingProduct is not null && editingIndex < 0)
        {
            ShowFormError("This product is no longer in the catalog. Close the form and try again.");
            return;
        }

        string? previousImageFileName = _editingProduct?.ImageFileName;
        string? savedImageFileName = _removeProductImage ? null : previousImageFileName;
        BitmapImage? savedImage = _removeProductImage ? null : _editingProduct?.Image;
        string? copiedImageFileName = null;
        if (_selectedProductImagePath is not null)
        {
            try
            {
                copiedImageFileName = _productImageStore.CopyImage(_selectedProductImagePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or FormatException)
            {
                ShowFormError($"Couldn't save the product photo. {exception.Message}");
                return;
            }

            savedImageFileName = copiedImageFileName;
            savedImage = _selectedProductImage;
        }

        Product savedProduct = new()
        {
            Name = name,
            Sku = sku,
            Category = category,
            Quantity = quantity,
            ReorderLevel = reorderLevel,
            Cost = cost,
            ImageFileName = savedImageFileName,
            Image = savedImage
        };

        List<Product> updatedProducts = _products.ToList();
        if (editingIndex >= 0)
        {
            updatedProducts[editingIndex] = savedProduct;
        }
        else
        {
            updatedProducts.Add(savedProduct);
        }

        List<StockMovement> updatedMovements = _movements.ToList();
        if (editingIndex < 0 && quantity > 0)
        {
            updatedMovements.Insert(0, new StockMovement
            {
                ProductName = savedProduct.Name,
                ProductSku = savedProduct.Sku,
                Action = "Opening stock",
                Reason = "Initial quantity when product was added",
                QuantityChange = quantity,
                QuantityAfter = quantity,
                OccurredAt = DateTimeOffset.UtcNow
            });
        }

        try
        {
            _inventoryStore.Save(new InventoryData
            {
                Products = updatedProducts,
                Movements = updatedMovements,
                Suppliers = _suppliers.ToList()
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            if (copiedImageFileName is not null)
            {
                try
                {
                    _productImageStore.DeleteImage(copiedImageFileName);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    ShowFormError($"Couldn't save the product. {exception.Message} The temporary image also couldn't be removed: {cleanupException.Message}");
                    return;
                }
            }

            ShowFormError($"Couldn't save the product. Check that the app-data folder is writable. {exception.Message}");
            return;
        }

        if (previousImageFileName is not null &&
            !string.Equals(previousImageFileName, savedImageFileName, StringComparison.Ordinal))
        {
            try
            {
                _productImageStore.DeleteImage(previousImageFileName);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"The product was saved, but its previous photo couldn't be removed.\n\n{exception.Message}",
                    "Old product photo couldn't be removed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        if (editingIndex >= 0)
        {
            _products[editingIndex] = savedProduct;
        }
        else
        {
            _products.Add(savedProduct);
            if (quantity > 0)
            {
                _movements.Insert(0, updatedMovements[0]);
            }
        }

        _productsView?.Refresh();
        RefreshInventorySummary();
        RefreshActivityLists();
        AddProductOverlay.Visibility = Visibility.Collapsed;
        _editingProduct = null;
        NavigateTo("Products");
        ProductsGrid.SelectedItem = savedProduct;
        ProductsGrid.ScrollIntoView(savedProduct);
    }

    private void RecordMovement_Click(object sender, RoutedEventArgs e)
    {
        if (_products.Count == 0)
        {
            MessageBox.Show(
                "Add a product before recording a stock change.",
                "No products in inventory",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MovementProductCombo.ItemsSource = _products;
        MovementProductCombo.DisplayMemberPath = nameof(Product.Name);
        MovementProductCombo.SelectedIndex = 0;
        MovementTypeCombo.SelectedIndex = 0;
        MovementQuantityBox.Clear();
        MovementReasonBox.Clear();
        MovementFormErrorText.Visibility = Visibility.Collapsed;
        UpdateMovementQuantityLabel();
        StockMovementOverlay.Visibility = Visibility.Visible;
    }

    private void CloseMovement_Click(object sender, RoutedEventArgs e)
    {
        StockMovementOverlay.Visibility = Visibility.Collapsed;
    }

    private void MovementType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMovementQuantityLabel();
    }

    private void UpdateMovementQuantityLabel()
    {
        if (MovementQuantityLabel is null || MovementTypeCombo is null)
        {
            return;
        }

        MovementQuantityLabel.Text = MovementTypeCombo.SelectedIndex switch
        {
            0 => "Quantity to receive",
            1 => "Quantity to use or remove",
            2 => "New stock count",
            _ => "Quantity"
        };
        MovementQuantityHelpText.Text = MovementTypeCombo.SelectedIndex == 2
            ? "Enter the counted quantity; the difference will be recorded."
            : "Enter a whole number greater than zero.";
    }

    private void SaveMovement_Click(object sender, RoutedEventArgs e)
    {
        if (MovementProductCombo.SelectedItem is not Product selectedProduct)
        {
            ShowMovementError("Choose a product.");
            return;
        }

        if (!int.TryParse(MovementQuantityBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int enteredQuantity))
        {
            ShowMovementError("Enter a whole-number quantity.");
            return;
        }

        string reason = MovementReasonBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            ShowMovementError("Enter a reason for this stock change.");
            return;
        }

        int quantityChange;
        int quantityAfter;
        string action;
        switch (MovementTypeCombo.SelectedIndex)
        {
            case 0:
                if (enteredQuantity <= 0)
                {
                    ShowMovementError("Quantity received must be greater than zero.");
                    return;
                }

                quantityChange = enteredQuantity;
                if (enteredQuantity > int.MaxValue - selectedProduct.Quantity)
                {
                    ShowMovementError("The resulting stock count is too large.");
                    return;
                }

                quantityAfter = selectedProduct.Quantity + enteredQuantity;
                action = "Received";
                break;
            case 1:
                if (enteredQuantity <= 0)
                {
                    ShowMovementError("Quantity used or removed must be greater than zero.");
                    return;
                }

                if (enteredQuantity > selectedProduct.Quantity)
                {
                    ShowMovementError($"Only {selectedProduct.Quantity} units are in stock.");
                    return;
                }

                quantityChange = -enteredQuantity;
                quantityAfter = selectedProduct.Quantity - enteredQuantity;
                action = "Used / removed";
                break;
            case 2:
                if (enteredQuantity < 0)
                {
                    ShowMovementError("The new stock count cannot be negative.");
                    return;
                }

                quantityAfter = enteredQuantity;
                quantityChange = quantityAfter - selectedProduct.Quantity;
                action = "Adjusted";
                break;
            default:
                ShowMovementError("Choose a stock-change type.");
                return;
        }

        Product updatedProduct = new()
        {
            Name = selectedProduct.Name,
            Sku = selectedProduct.Sku,
            Category = selectedProduct.Category,
            Quantity = quantityAfter,
            ReorderLevel = selectedProduct.ReorderLevel,
            Cost = selectedProduct.Cost
        };
        StockMovement movement = new()
        {
            ProductName = selectedProduct.Name,
            ProductSku = selectedProduct.Sku,
            Action = action,
            Reason = reason,
            QuantityChange = quantityChange,
            QuantityAfter = quantityAfter,
            OccurredAt = DateTimeOffset.UtcNow
        };

        int productIndex = _products.IndexOf(selectedProduct);
        if (productIndex < 0)
        {
            ShowMovementError("The selected product is no longer in the inventory. Close this form and try again.");
            return;
        }

        List<Product> updatedProducts = _products.ToList();
        updatedProducts[productIndex] = updatedProduct;
        List<StockMovement> updatedMovements = [movement, .. _movements];
        try
        {
            _inventoryStore.Save(new InventoryData
            {
                Products = updatedProducts,
                Movements = updatedMovements,
                Suppliers = _suppliers.ToList()
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ShowMovementError($"Couldn't save this stock change. The quantity was not changed. {exception.Message}");
            return;
        }

        _products[productIndex] = updatedProduct;
        _movements.Insert(0, movement);
        _productsView?.Refresh();
        RefreshInventorySummary();
        RefreshActivityLists();
        StockMovementOverlay.Visibility = Visibility.Collapsed;
        NavigateTo("Activity");
    }

    private void ShowMovementError(string message)
    {
        MovementFormErrorText.Text = message;
        MovementFormErrorText.Visibility = Visibility.Visible;
    }

    private void ShowFormError(string message)
    {
        FormErrorText.Text = message;
        FormErrorText.Visibility = Visibility.Visible;
    }

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        SupplierNameBox.Clear();
        SupplierContactBox.Clear();
        SupplierEmailBox.Clear();
        SupplierPhoneBox.Clear();
        SupplierFormErrorText.Visibility = Visibility.Collapsed;
        AddSupplierOverlay.Visibility = Visibility.Visible;
        SupplierNameBox.Focus();
    }

    private void CloseSupplier_Click(object sender, RoutedEventArgs e)
    {
        AddSupplierOverlay.Visibility = Visibility.Collapsed;
    }

    private void SaveSupplier_Click(object sender, RoutedEventArgs e)
    {
        string name = SupplierNameBox.Text.Trim();
        string contact = SupplierContactBox.Text.Trim();
        string email = SupplierEmailBox.Text.Trim();
        string phone = SupplierPhoneBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowSupplierError("Enter a supplier name.");
            return;
        }

        if (_suppliers.Any(supplier => string.Equals(supplier.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowSupplierError("A supplier with that name already exists.");
            return;
        }

        if (email.Length > 0 && !MailAddress.TryCreate(email, out _))
        {
            ShowSupplierError("Enter a valid email address, or leave the email field blank.");
            return;
        }

        Supplier supplierToAdd = new()
        {
            Name = name,
            ContactName = contact,
            Email = email,
            Phone = phone
        };
        List<Supplier> updatedSuppliers = _suppliers.Append(supplierToAdd)
            .OrderBy(supplier => supplier.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        try
        {
            _inventoryStore.Save(new InventoryData
            {
                Products = _products.ToList(),
                Movements = _movements.ToList(),
                Suppliers = updatedSuppliers
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ShowSupplierError($"Couldn't save the supplier. {exception.Message}");
            return;
        }

        int insertIndex = _suppliers.TakeWhile(supplier =>
            string.Compare(supplier.Name, supplierToAdd.Name, StringComparison.CurrentCultureIgnoreCase) < 0).Count();
        _suppliers.Insert(insertIndex, supplierToAdd);
        RefreshInventorySummary();
        AddSupplierOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowSupplierError(string message)
    {
        SupplierFormErrorText.Text = message;
        SupplierFormErrorText.Visibility = Visibility.Visible;
    }

    private void ExportInventoryReport_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Export inventory report",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"Stockpile-System-inventory-{DateTime.Now:yyyy-MM-dd}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        StringBuilder csv = new();
        csv.AppendLine("Product,SKU,Category,Quantity,Reorder level,Unit cost,Stock value");
        foreach (Product product in _products.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            decimal stockValue = product.Quantity * product.Cost;
            csv.Append(EscapeCsv(product.Name)).Append(',')
                .Append(EscapeCsv(product.Sku)).Append(',')
                .Append(EscapeCsv(product.Category)).Append(',')
                .Append(product.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(product.ReorderLevel.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(product.Cost.ToString(CultureInfo.InvariantCulture)).Append(',')
                .AppendLine(stockValue.ToString(CultureInfo.InvariantCulture));
        }

        try
        {
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show(
                $"Inventory report saved to:\n{dialog.FileName}",
                "Report exported",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Couldn't export the report to:\n{dialog.FileName}\n\n{exception.Message}",
                "Report export failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string EscapeCsv(string value)
    {
        string formulaSafeValue = value.TrimStart().Length > 0 && "=-+@".Contains(value.TrimStart()[0])
            ? $"'{value}"
            : value;
        return $"\"{formulaSafeValue.Replace("\"", "\"\"")}\"";
    }

    private void RefreshInventorySummary()
    {
        ProductsCountText.Text = _products.Count.ToString("N0", CultureInfo.CurrentCulture);
        TotalUnitsOnHandText.Text = _products.Sum(product => (long)product.Quantity)
            .ToString("N0", CultureInfo.CurrentCulture);
        LowStockCountText.Text = _products.Count(product => product.Quantity <= product.ReorderLevel)
            .ToString(CultureInfo.CurrentCulture);
        StockValueText.Text = _products.Sum(product => product.Quantity * product.Cost)
            .ToString("C0", CultureInfo.CurrentCulture);
        MovementCountText.Text = _movements.Count(movement => movement.OccurredAt >= DateTimeOffset.UtcNow.AddDays(-7))
            .ToString(CultureInfo.CurrentCulture);
        List<Product> lowStockProducts = _products
            .Where(product => product.Quantity <= product.ReorderLevel)
            .Take(3)
            .ToList();
        LowStockList.ItemsSource = lowStockProducts;
        NoLowStockText.Visibility = lowStockProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshStockLevelChart();
        SupplierCountText.Text = _suppliers.Count.ToString("N0", CultureInfo.CurrentCulture);
        ReportProductsCountText.Text = _products.Count.ToString("N0", CultureInfo.CurrentCulture);
        ReportStockValueText.Text = StockValueText.Text;
        ReportLowStockCountText.Text = _products.Count(product => product.Quantity <= product.ReorderLevel)
            .ToString(CultureInfo.CurrentCulture);
        ReportMovementCountText.Text = _movements.Count(movement => movement.OccurredAt >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToString(CultureInfo.CurrentCulture);
        SupplierCountText.Text = $"{_suppliers.Count:N0} suppliers";
        NoSuppliersText.Visibility = _suppliers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CategorySummaryGrid.ItemsSource = _products
            .GroupBy(product => product.Category, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new CategoryStockSummary
            {
                Category = group.Key,
                ProductCount = group.Count(),
                UnitsInStock = group.Sum(product => (long)product.Quantity),
                StockValue = group.Sum(product => product.Quantity * product.Cost)
            })
            .OrderByDescending(summary => summary.StockValue)
            .ToList();
        RefreshActivityChart();
    }

    private void RefreshStockLevelChart()
    {
        int[] counts =
        [
            _products.Count(product => product.Quantity > product.ReorderLevel),
            _products.Count(product => product.Quantity > 0 && product.Quantity <= product.ReorderLevel),
            _products.Count(product => product.Quantity == 0)
        ];
        System.Windows.Shapes.Path[] arcs = [InStockArc, LowStockArc, OutOfStockArc];
        TextBlock[] countLabels = [InStockCountText, LowStockStatusCountText, OutOfStockCountText];
        int total = _products.Count;
        int visibleSlices = counts.Count(count => count > 0);
        double gap = visibleSlices > 1 ? 4 : 0;
        double availableDegrees = 360 - gap * visibleSlices;
        double angle = -90;

        StockLevelTotalText.Text = total.ToString("N0", CultureInfo.CurrentCulture);
        for (int index = 0; index < counts.Length; index++)
        {
            int count = counts[index];
            countLabels[index].Text = count.ToString("N0", CultureInfo.CurrentCulture);
            if (count == 0 || total == 0)
            {
                arcs[index].Data = Geometry.Empty;
                continue;
            }

            double sweep = availableDegrees * count / total;
            arcs[index].Data = CreateDonutArc(angle + gap / 2, sweep);
            angle += sweep + gap;
        }
    }

    private static Geometry CreateDonutArc(double startAngle, double sweepAngle)
    {
        const double center = 80;
        const double radius = 64;

        Point PointAt(double angle)
        {
            double radians = angle * Math.PI / 180;
            return new Point(
                center + radius * Math.Cos(radians),
                center + radius * Math.Sin(radians));
        }

        PathFigure figure = new()
        {
            StartPoint = PointAt(startAngle)
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointAt(startAngle + sweepAngle),
            Size = new Size(radius, radius),
            IsLargeArc = sweepAngle > 180,
            SweepDirection = SweepDirection.Clockwise
        });

        PathGeometry geometry = new();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void RefreshActivityLists()
    {
        RecentMovementsList.ItemsSource = _movements.Take(3).ToList();
        NoActivityText.Visibility = _movements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshActivityChart()
    {
        DateTime firstDay = DateTime.Today.AddDays(-6);
        Dictionary<DateTime, (long Received, long Used)> dailyTotals = _movements
            .Where(movement => movement.OccurredAt.ToLocalTime().Date >= firstDay)
            .GroupBy(movement => movement.OccurredAt.ToLocalTime().Date)
            .ToDictionary(
                group => group.Key,
                group => (
                    Received: group.Where(movement => movement.QuantityChange > 0).Sum(movement => (long)movement.QuantityChange),
                    Used: group.Where(movement => movement.QuantityChange < 0).Sum(movement => -(long)movement.QuantityChange)));

        long maxDailyMovement = Math.Max(
            1,
            dailyTotals.Values.SelectMany(total => new[] { total.Received, total.Used }).DefaultIfEmpty(0).Max());
        double scale = 120d / maxDailyMovement;
        List<ActivityDay> days = Enumerable.Range(0, 7)
            .Select(offset => firstDay.AddDays(offset))
            .Select(day =>
            {
                dailyTotals.TryGetValue(day, out (long Received, long Used) total);
                return new ActivityDay
                {
                    DayLabel = day.ToString("ddd", CultureInfo.CurrentCulture),
                    ReceivedHeight = total.Received == 0 ? 2 : Math.Max(4, total.Received * scale),
                    UsedHeight = total.Used == 0 ? 2 : Math.Max(4, total.Used * scale)
                };
            })
            .ToList();

        ActivityChartItems.ItemsSource = days;
        ActivityChartEmpty.Visibility = dailyTotals.Values.Any(total => total.Received > 0 || total.Used > 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Appearance_Click(object sender, RoutedEventArgs e)
    {
        AppearancePanel.Visibility = AppearancePanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string colorText)
        {
            return;
        }

        if (ColorConverter.ConvertFromString(colorText) is not Color accent)
        {
            return;
        }

        ApplyAccent(accent);
    }

    private void CustomAccent_Click(object sender, RoutedEventArgs e)
    {
        string value = CustomAccentBox.Text.Trim();
        if (value.Length != 7 || !value.StartsWith('#')
            || ColorConverter.ConvertFromString(value) is not Color color)
        {
            ShowAccentError("Enter a hex color in #RRGGBB format.");
            return;
        }

        if (Math.Max(GetContrastRatio(color, Colors.White), GetContrastRatio(color, Colors.Black)) < 4.5)
        {
            ShowAccentError("Choose a darker or lighter color for readable button text.");
            return;
        }

        ApplyAccent(color);
    }

    private void ShowAccentError(string message)
    {
        AccentErrorText.Text = message;
        AccentErrorText.Visibility = Visibility.Visible;
    }

    private static double GetContrastRatio(Color background, Color foreground)
    {
        double backgroundLuminance = GetRelativeLuminance(background);
        double foregroundLuminance = GetRelativeLuminance(foreground);
        double lighter = Math.Max(backgroundLuminance, foregroundLuminance);
        double darker = Math.Min(backgroundLuminance, foregroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        static double Linearize(byte channel)
        {
            double value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);
    }

    private void ApplyAccent(Color accent)
    {
        AppearanceSettings updatedSettings = new()
        {
            AccentColor = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}",
            IsDarkTheme = _isDarkTheme
        };

        if (!TrySaveAppearance(updatedSettings))
        {
            return;
        }

        ApplyAccentResources(accent);
        CustomAccentBox.Text = updatedSettings.AccentColor;
        AccentErrorText.Visibility = Visibility.Collapsed;
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        bool newThemeIsDark = !_isDarkTheme;
        AppearanceSettings updatedSettings = new()
        {
            AccentColor = _appearanceSettings.AccentColor,
            IsDarkTheme = newThemeIsDark
        };

        if (!TrySaveAppearance(updatedSettings))
        {
            return;
        }

        _isDarkTheme = newThemeIsDark;
        ApplyThemeResources(_isDarkTheme);
        UpdateThemeToggleLabel();
    }

    private bool TrySaveAppearance(AppearanceSettings settings)
    {
        try
        {
            _appearanceSettingsStore.Save(settings);
            _appearanceSettings = settings;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(
                $"Stockpile System couldn't save your appearance preferences to:\n{_appearanceSettingsStore.FilePath}\n\n{exception.Message}",
                "Appearance preferences couldn't be saved",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private void UpdateThemeToggleLabel()
    {
        ThemeToggleButton.Content = _isDarkTheme ? "Switch to light theme" : "Switch to dark theme";
    }

    private static void ApplyAccentResources(Color accent)
    {
        Application.Current.Resources["AccentBrush"] = new SolidColorBrush(accent);
        Application.Current.Resources["AccentSoftBrush"] =
            new SolidColorBrush(Color.FromArgb(36, accent.R, accent.G, accent.B));
        Application.Current.Resources["AccentForegroundBrush"] =
            new SolidColorBrush(GetContrastRatio(accent, Colors.White) >= GetContrastRatio(accent, Colors.Black)
                ? Colors.White
                : Colors.Black);
    }

    private static void ApplyThemeResources(bool isDarkTheme)
    {
        SetBrush("AppBackgroundBrush", isDarkTheme ? "#111A20" : "#F5F7FB");
        SetBrush("SurfaceBrush", isDarkTheme ? "#18252D" : "#FFFFFF");
        SetBrush("TextPrimaryBrush", isDarkTheme ? "#F2F7F5" : "#101828");
        SetBrush("TextMutedBrush", isDarkTheme ? "#9EB0B3" : "#667085");
        SetBrush("BorderBrush", isDarkTheme ? "#2A3A40" : "#EAECF0");
        SetBrush("WarningBrush", isDarkTheme ? "#F3C84B" : "#B54708");
    }

    private static void SetBrush(string key, string colorText)
    {
        if (ColorConverter.ConvertFromString(colorText) is Color color)
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }
}
