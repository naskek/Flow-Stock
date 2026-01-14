namespace LightWms.Core.Models;

public sealed class DocLineView
{
    public long Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public double Qty { get; init; }
    public string? FromLocation { get; init; }
    public string? ToLocation { get; init; }
}
