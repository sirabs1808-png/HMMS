using System.IO;
using System.Text.Json;
using StockPilot.Models;

namespace StockPilot.Services;

public sealed class AppearanceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppearanceSettingsStore()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = Path.Combine(appDataPath, "StockPilot", "appearance.json");
    }

    public string FilePath { get; }

    public AppearanceSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppearanceSettings();
        }

        using FileStream stream = File.OpenRead(FilePath);
        AppearanceSettings settings = JsonSerializer.Deserialize<AppearanceSettings>(stream, JsonOptions)
            ?? throw new InvalidDataException("The appearance settings file is empty or invalid.");

        if (string.IsNullOrEmpty(settings.AccentColor)
            || settings.AccentColor.Length != 7
            || settings.AccentColor[0] != '#'
            || !settings.AccentColor.AsSpan(1).ToString().All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("The saved accent color must use #RRGGBB format.");
        }

        return settings;
    }

    public void Save(AppearanceSettings settings)
    {
        if (string.IsNullOrEmpty(settings.AccentColor)
            || settings.AccentColor.Length != 7
            || settings.AccentColor[0] != '#'
            || !settings.AccentColor.AsSpan(1).ToString().All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("The accent color must use #RRGGBB format.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporaryPath = $"{FilePath}.tmp";
        using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, settings, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
