using FlowStock.Core.Models;
using FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

namespace FlowStock.Server.Tests.UpdateDocLine;

public sealed class CanonicalUpdateIntegrationTests
{
    [Fact]
    public async Task SuccessfulUpdate_CreatesReplacementRowAndKeepsOriginalHistory()
    {
        var scenario = await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-update-can-001",
            "evt-line-update-create-001",
            "evt-line-update-add-001");
        await using var host = scenario.Host;

        var payload = await UpdateDocLineHttpApi.UpdateAsync(
            host.Client,
            scenario.DocUid,
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-001",
                DeviceId = "API-01",
                LineId = scenario.LineId,
                Qty = 12,
                UomCode = "BOX"
            });

        Assert.True(payload.Ok);
        Assert.Equal("UPDATED", payload.Result);
        Assert.True(payload.Appended);
        Assert.False(payload.IdempotentReplay);
        Assert.NotNull(payload.Line);
        Assert.NotEqual(scenario.LineId, payload.Line!.Id);
        Assert.Equal(scenario.LineId, payload.Line.ReplacesLineId);
        Assert.Equal(12, payload.Line.Qty);

        var activeLines = scenario.Harness.GetDocLines(scenario.DocId);
        var allLines = scenario.Harness.GetAllDocLines(scenario.DocId);

        var active = Assert.Single(activeLines);
        Assert.Equal(payload.Line.Id, active.Id);
        Assert.Equal(scenario.LineId, active.ReplacesLineId);
        Assert.Equal(12, active.Qty);

        Assert.Equal(2, allLines.Count);
        Assert.Contains(allLines, line => line.Id == scenario.LineId && line.Qty == 5);
        Assert.Contains(allLines, line => line.Id == payload.Line.Id && line.Qty == 12 && line.ReplacesLineId == scenario.LineId);
    }

    [Fact]
    public async Task Update_DoesNotWriteLedger_AndKeepsDraftStatus()
    {
        var scenario = await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-update-can-002",
            "evt-line-update-create-002",
            "evt-line-update-add-002");
        await using var host = scenario.Host;

        await UpdateDocLineHttpApi.UpdateAsync(
            host.Client,
            scenario.DocUid,
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-002",
                DeviceId = "API-01",
                LineId = scenario.LineId,
                Qty = 9
            });

        var doc = scenario.Harness.GetDoc(scenario.DocId);
        Assert.Equal(DocStatus.Draft, doc.Status);
        Assert.Null(doc.ClosedAt);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }
}
