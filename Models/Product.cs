using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace StockPilot.Models;

public sealed class Product
{
    public required string Name { get; init; }
    public required string Sku { get; init; }
    public required string Category { get; init; }
    public int Quantity { get; init; }
    public int ReorderLevel { get; init; }
    public decimal Cost { get; init; }
    public string? ImageFileName { get; init; }

    [JsonIgnore]
    public BitmapImage? Image { get; set; }

    public string Initials
    {
        get
        {
            string[] words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return "?";
            }

            return words.Length == 1
                ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant()
                : string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[1][0]));
        }
    }
}
