namespace FlowStock.Core.Models;

public sealed class KmCodeBatch
{
    public long Id { get; init; }
    public long? OrderId { get; init; }
    public string? OrderRef { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public DateTime ImportedAt { get; init; }
    public string? ImportedBy { get; init; }
    public int TotalCodes { get; init; }
    public int ErrorCount { get; init; }
}
