using System.IO;
using System.Windows.Media.Imaging;

namespace StockPilot.Services;

public sealed class ProductImageStore
{
    private const long MaximumImageSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".tif",
        ".tiff"
    };

    public ProductImageStore()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ImageDirectory = Path.Combine(appDataPath, "StockPilot", "product-images");
    }

    public string ImageDirectory { get; }

    public BitmapImage? LoadManagedImage(string? fileName)
    {
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : LoadImage(ResolveManagedPath(fileName));
    }

    public BitmapImage LoadPreviewImage(string sourcePath)
    {
        ValidateImageFile(sourcePath);

        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 256;
        image.UriSource = new Uri(Path.GetFullPath(sourcePath), UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public string CopyImage(string sourcePath)
    {
        _ = LoadPreviewImage(sourcePath);
        Directory.CreateDirectory(ImageDirectory);

        string fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        File.Copy(sourcePath, Path.Combine(ImageDirectory, fileName), overwrite: false);
        return fileName;
    }

    public void DeleteImage(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        string path = ResolveManagedPath(fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private BitmapImage LoadImage(string path)
    {
        return LoadPreviewImage(path);
    }

    private string ResolveManagedPath(string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || !SupportedExtensions.Contains(Path.GetExtension(fileName)))
        {
            throw new InvalidDataException("The saved product image reference is invalid.");
        }

        return Path.Combine(ImageDirectory, fileName);
    }

    private static void ValidateImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException("Choose a product image file.");
        }

        FileInfo file = new(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The selected product image could not be found.", path);
        }

        if (!SupportedExtensions.Contains(file.Extension))
        {
            throw new InvalidDataException("Choose a PNG, JPEG, BMP, GIF, or TIFF image.");
        }

        if (file.Length == 0 || file.Length > MaximumImageSize)
        {
            throw new InvalidDataException("Product images must be smaller than 10 MB.");
        }
    }
}
