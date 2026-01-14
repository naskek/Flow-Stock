namespace LightWms.Core.Models;

public sealed class Item
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string? Gtin { get; init; }
    public string? Uom { get; init; }
}
