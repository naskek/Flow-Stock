using FlowStock.Core.Models;
using FlowStock.Server;
using FlowStock.Server.Tests.DeleteDocLine.Infrastructure;

namespace FlowStock.Server.Tests.DeleteDocLine;

public sealed class CanonicalDeleteIntegrationTests
{
    [Fact]
    public async Task SuccessfulDelete_CreatesTombstoneAndRemovesActiveLine()
    {
        var scenario = await DeleteDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-delete-can-001",
            "evt-line-delete-create-001",
            "evt-line-delete-add-001");
        await using var host = scenario.Host;

        var payload = await DeleteDocLineHttpApi.DeleteAsync(
            host.Client,
            scenario.DocUid,
            new DeleteDocLineRequest
            {
                EventId = "evt-line-delete-001",
                DeviceId = "API-01",
                LineId = scenario.LineId
            });

        Assert.True(payload.Ok);
        Assert.Equal("DELETED", payload.Result);
        Assert.True(payload.Appended);
        Assert.False(payload.IdempotentReplay);
        Assert.NotNull(payload.Line);
        Assert.NotEqual(scenario.LineId, payload.Line!.Id);
        Assert.Equal(scenario.LineId, payload.Line.ReplacesLineId);
        Assert.Equal(0, payload.Line.Qty);

        Assert.Empty(scenario.Harness.GetDocLines(scenario.DocId));
        var allLines = scenario.Harness.GetAllDocLines(scenario.DocId);
        Assert.Equal(2, allLines.Count);
        Assert.Contains(allLines, line => line.Id == scenario.LineId && line.Qty == 5);
        Assert.Contains(allLines, line => line.Id == payload.Line.Id && line.Qty == 0 && line.ReplacesLineId == scenario.LineId);
        Assert.Equal(1, scenario.ApiStore.CountEvents("DOC_LINE_DELETE", scenario.DocUid));
    }

    [Fact]
    public async Task Delete_DoesNotWriteLedger_AndKeepsDraftStatus()
    {
        var scenario = await DeleteDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-delete-can-002",
            "evt-line-delete-create-002",
            "evt-line-delete-add-002");
        await using var host = scenario.Host;

        await DeleteDocLineHttpApi.DeleteAsync(
            host.Client,
            scenario.DocUid,
            new DeleteDocLineRequest
            {
                EventId = "evt-line-delete-002",
                DeviceId = "API-01",
                LineId = scenario.LineId
            });

        var doc = scenario.Harness.GetDoc(scenario.DocId);
        Assert.Equal(DocStatus.Draft, doc.Status);
        Assert.Null(doc.ClosedAt);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }
}
