using System.Text.Json;
using System.Text.Json.Serialization;
using FlowStock.Server;

namespace FlowStock.Server.Tests.CreateOrder.Infrastructure;

[CollectionDefinition("CreateOrder", DisableParallelization = true)]
public sealed class CreateOrderCollectionDefinition : ICollectionFixture<CreateOrderPartnerStatusesFixture>
{
}

public sealed class CreateOrderPartnerStatusesFixture : IDisposable
{
    private readonly string _path;
    private readonly string? _originalContent;
    private readonly bool _hadOriginalFile;

    public CreateOrderPartnerStatusesFixture()
    {
        Directory.CreateDirectory(ServerPaths.BaseDir);
        _path = Path.Combine(ServerPaths.BaseDir, "partner_statuses.json");
        _hadOriginalFile = File.Exists(_path);
        _originalContent = _hadOriginalFile ? File.ReadAllText(_path) : null;

        var json = JsonSerializer.Serialize(
            new Dictionary<long, PartnerRoleValue>
            {
                [200] = PartnerRoleValue.Client,
                [201] = PartnerRoleValue.Supplier
            },
            new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                WriteIndented = false
            });

        File.WriteAllText(_path, json);
    }

    public void Dispose()
    {
        if (_hadOriginalFile)
        {
            File.WriteAllText(_path, _originalContent ?? string.Empty);
            return;
        }

        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private enum PartnerRoleValue
    {
        Client,
        Supplier,
        Both
    }
}
