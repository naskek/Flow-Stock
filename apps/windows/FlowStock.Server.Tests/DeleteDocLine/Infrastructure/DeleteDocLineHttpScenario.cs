using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

namespace FlowStock.Server.Tests.DeleteDocLine.Infrastructure;

internal static class DeleteDocLineHttpScenario
{
    public static async Task<(CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, CloseDocumentHttpHost Host, long DocId, long LineId, string DocUid)> StartInboundDraftWithLineAsync(
        string docUid,
        string createEventId,
        string addEventId)
    {
        return await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(docUid, createEventId, addEventId);
    }
}
