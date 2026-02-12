namespace FlowStock.Core.Models;

public sealed class KmImportResult
{
    public long? BatchId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public bool IsDuplicateFile { get; init; }
    public int Imported { get; init; }
    public int Duplicates { get; init; }
    public int Errors { get; init; }
    public int InvalidGtins { get; init; }
    public int EmptyCodes { get; init; }
    public int UnmatchedSku { get; init; }
}
