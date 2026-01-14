namespace LightWms.Core.Models;

public sealed class ImportResult
{
    public int Imported { get; set; }
    public int Duplicates { get; set; }
    public int Errors { get; set; }
}
