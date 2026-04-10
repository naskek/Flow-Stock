namespace FlowStock.Server;

public static class ServerPaths
{
    private const string TsdRootEnvKey = "FLOWSTOCK_TSD_ROOT";
    private const string PcRootEnvKey = "FLOWSTOCK_PC_ROOT";

    public static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FlowStock");

    public static string TsdRoot => ResolveTsdRoot();
    public static string PcRoot => ResolvePcRoot();

    private static string ResolveTsdRoot()
    {
        var configured = Environment.GetEnvironmentVariable(TsdRootEnvKey);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        return ResolveProjectRelativePath("android", "tsd");
    }

    private static string ResolvePcRoot()
    {
        var configured = Environment.GetEnvironmentVariable(PcRootEnvKey);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        return ResolveProjectRelativePath("android", "tsd", "pc");
    }

    private static string ResolveProjectRelativePath(params string[] segments)
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var allSegments = new List<string> { projectDir, "..", ".." };
        allSegments.AddRange(segments);
        return Path.GetFullPath(Path.Combine(allSegments.ToArray()));
    }
}

