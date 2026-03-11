using FlowStock.Core.Models;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.CreateDocLine.Infrastructure;

namespace FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

internal static class UpdateDocLineHttpScenario
{
    public static async Task<(CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, CloseDocumentHttpHost Host, long DocId, long LineId, string DocUid)> StartInboundDraftWithLineAsync(
        string docUid,
        string createEventId,
        string addEventId)
    {
        var (harness, apiStore) = CreateDocLineHttpScenario.CreateInboundScenario();
        var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);
        var created = await CreateDocLineHttpScenario.CreateInboundDraftAsync(host.Client, docUid, createEventId);
        var docId = created.Doc!.Id;

        var appended = await CreateDocLineHttpApi.AddAsync(
            host.Client,
            docUid,
            new AddDocLineRequest
            {
                EventId = addEventId,
                DeviceId = "API-01",
                ItemId = 100,
                Qty = 5,
                UomCode = "BOX"
            });

        return (harness, apiStore, host, docId, appended.Line!.Id, docUid);
    }
}
