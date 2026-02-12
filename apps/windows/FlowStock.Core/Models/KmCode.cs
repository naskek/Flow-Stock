namespace FlowStock.Core.Models;

public sealed class KmCode
{
    public long Id { get; init; }
    public long BatchId { get; init; }
    public string CodeRaw { get; init; } = string.Empty;
    public string? Gtin14 { get; init; }
    public long? SkuId { get; init; }
    public string? SkuName { get; init; }
    public string? SkuBarcode { get; init; }
    public string? ProductName { get; init; }
    public KmCodeStatus Status { get; init; }
    public long? ReceiptDocId { get; init; }
    public long? ReceiptLineId { get; init; }
    public long? HuId { get; init; }
    public string? HuCode { get; init; }
    public long? LocationId { get; init; }
    public string? LocationCode { get; init; }
    public long? ShipDocId { get; init; }
    public long? ShipLineId { get; init; }
    public long? OrderId { get; init; }
}
