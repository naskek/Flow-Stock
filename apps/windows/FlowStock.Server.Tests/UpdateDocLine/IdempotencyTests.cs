using System.Net;
using System.Net.Http.Json;
using FlowStock.Core.Models;
using FlowStock.Server;
using FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

namespace FlowStock.Server.Tests.UpdateDocLine;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task SameEventIdSamePayload_ReturnsIdempotentReplay()
    {
        var scenario = await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-update-idem-001",
            "evt-line-update-create-101",
            "evt-line-update-add-101");
        await using var host = scenario.Host;

        var request = new UpdateDocLineRequest
        {
            EventId = "evt-line-update-idem-001",
            DeviceId = "API-01",
            LineId = scenario.LineId,
            Qty = 12,
            UomCode = "BOX"
        };

        var first = await UpdateDocLineHttpApi.UpdateAsync(host.Client, scenario.DocUid, request);
        var second = await UpdateDocLineHttpApi.UpdateAsync(host.Client, scenario.DocUid, request);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal("UPDATED", first.Result);
        Assert.Equal("IDEMPOTENT_REPLAY", second.Result);
        Assert.False(first.IdempotentReplay);
        Assert.True(second.IdempotentReplay);
        Assert.False(second.Appended);
        Assert.NotNull(first.Line);
        Assert.NotNull(second.Line);
        Assert.Equal(first.Line!.Id, second.Line!.Id);
        Assert.Equal(first.Line.ReplacesLineId, second.Line.ReplacesLineId);

        Assert.Single(scenario.Harness.GetDocLines(scenario.DocId));
        Assert.Equal(2, scenario.Harness.GetAllDocLines(scenario.DocId).Count);
        Assert.Equal(1, scenario.ApiStore.CountEvents("DOC_LINE_UPDATE", scenario.DocUid));
        Assert.Equal(DocStatus.Draft, scenario.Harness.GetDoc(scenario.DocId).Status);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }

    [Fact]
    public async Task SameEventIdDifferentPayload_ReturnsEventIdConflict()
    {
        var scenario = await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-update-idem-002",
            "evt-line-update-create-102",
            "evt-line-update-add-102");
        await using var host = scenario.Host;

        await UpdateDocLineHttpApi.UpdateAsync(
            host.Client,
            scenario.DocUid,
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-idem-002",
                DeviceId = "API-01",
                LineId = scenario.LineId,
                Qty = 12
            });

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/docs/{scenario.DocUid}/lines/update",
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-idem-002",
                DeviceId = "API-01",
                LineId = scenario.LineId,
                Qty = 13
            });

        var payload = await UpdateDocLineHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("EVENT_ID_CONFLICT", payload.Error);
        Assert.Single(scenario.Harness.GetDocLines(scenario.DocId));
        Assert.Equal(2, scenario.Harness.GetAllDocLines(scenario.DocId).Count);
        Assert.Equal(1, scenario.ApiStore.CountEvents("DOC_LINE_UPDATE", scenario.DocUid));
        Assert.Equal(DocStatus.Draft, scenario.Harness.GetDoc(scenario.DocId).Status);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }
}
