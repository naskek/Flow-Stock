namespace LightWms.Core.Models;

public sealed class DocLine
{
    public long Id { get; init; }
    public long DocId { get; init; }
    public long ItemId { get; init; }
    public double Qty { get; init; }
    public long? FromLocationId { get; init; }
    public long? ToLocationId { get; init; }
}
