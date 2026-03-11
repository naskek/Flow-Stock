using System.Net;
using System.Net.Http.Json;
using FlowStock.Core.Models;
using FlowStock.Server;
using FlowStock.Server.Tests.DeleteDocLine.Infrastructure;

namespace FlowStock.Server.Tests.DeleteDocLine;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task SameEventIdSamePayload_ReturnsIdempotentReplay()
    {
        var scenario = await DeleteDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-delete-idem-001",
            "evt-line-delete-create-101",
            "evt-line-delete-add-101");
        await using var host = scenario.Host;

        var request = new DeleteDocLineRequest
        {
            EventId = "evt-line-delete-idem-001",
            DeviceId = "API-01",
            LineId = scenario.LineId
        };

        var first = await DeleteDocLineHttpApi.DeleteAsync(host.Client, scenario.DocUid, request);
        var second = await DeleteDocLineHttpApi.DeleteAsync(host.Client, scenario.DocUid, request);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal("DELETED", first.Result);
        Assert.Equal("IDEMPOTENT_REPLAY", second.Result);
        Assert.False(first.IdempotentReplay);
        Assert.True(second.IdempotentReplay);
        Assert.False(second.Appended);
        Assert.NotNull(first.Line);
        Assert.NotNull(second.Line);
        Assert.Equal(first.Line!.Id, second.Line!.Id);
        Assert.Equal(first.Line.ReplacesLineId, second.Line.ReplacesLineId);

        Assert.Empty(scenario.Harness.GetDocLines(scenario.DocId));
        Assert.Equal(2, scenario.Harness.GetAllDocLines(scenario.DocId).Count);
        Assert.Equal(1, scenario.ApiStore.CountEvents("DOC_LINE_DELETE", scenario.DocUid));
        Assert.Equal(DocStatus.Draft, scenario.Harness.GetDoc(scenario.DocId).Status);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }

    [Fact]
    public async Task SameEventIdDifferentPayload_ReturnsEventIdConflict()
    {
        var scenario = await DeleteDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-delete-idem-002",
            "evt-line-delete-create-102",
            "evt-line-delete-add-102");
        await using var host = scenario.Host;

        await DeleteDocLineHttpApi.DeleteAsync(
            host.Client,
            scenario.DocUid,
            new DeleteDocLineRequest
            {
                EventId = "evt-line-delete-idem-002",
                DeviceId = "API-01",
                LineId = scenario.LineId
            });

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/docs/{scenario.DocUid}/lines/delete",
            new DeleteDocLineRequest
            {
                EventId = "evt-line-delete-idem-002",
                DeviceId = "API-01",
                LineId = scenario.LineId + 1
            });

        var payload = await DeleteDocLineHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("EVENT_ID_CONFLICT", payload.Error);
        Assert.Empty(scenario.Harness.GetDocLines(scenario.DocId));
        Assert.Equal(2, scenario.Harness.GetAllDocLines(scenario.DocId).Count);
        Assert.Equal(1, scenario.ApiStore.CountEvents("DOC_LINE_DELETE", scenario.DocUid));
        Assert.Equal(DocStatus.Draft, scenario.Harness.GetDoc(scenario.DocId).Status);
        Assert.Empty(scenario.Harness.LedgerEntries);
    }
}
