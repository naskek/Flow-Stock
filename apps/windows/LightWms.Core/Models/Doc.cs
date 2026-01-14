namespace LightWms.Core.Models;

public sealed class Doc
{
    public long Id { get; init; }
    public string DocRef { get; init; } = string.Empty;
    public DocType Type { get; init; }
    public DocStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}
