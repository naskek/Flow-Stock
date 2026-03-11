using System.Net;
using System.Net.Http.Json;
using FlowStock.Core.Models;
using FlowStock.Server;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.UpdateDocLine.Infrastructure;

namespace FlowStock.Server.Tests.UpdateDocLine;

public sealed class ValidationTests
{
    [Fact]
    public async Task UnknownLineId_Fails()
    {
        var scenario = await UpdateDocLineHttpScenario.StartInboundDraftWithLineAsync(
            "line-update-val-001",
            "evt-line-update-create-201",
            "evt-line-update-add-201");
        await using var host = scenario.Host;

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/docs/{scenario.DocUid}/lines/update",
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-val-001",
                DeviceId = "API-01",
                LineId = 999999,
                Qty = 8
            });

        var payload = await UpdateDocLineHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("UNKNOWN_LINE", payload.Error);
        Assert.Single(scenario.Harness.GetDocLines(scenario.DocId));
        Assert.Single(scenario.Harness.GetAllDocLines(scenario.DocId));
    }

    [Fact]
    public async Task NonDraftDocument_Fails()
    {
        var (harness, apiStore, docUid) = CreateClosedScenarioWithLine();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/docs/{docUid}/lines/update",
            new UpdateDocLineRequest
            {
                EventId = "evt-line-update-val-002",
                DeviceId = "API-01",
                LineId = 1,
                Qty = 8
            });

        var payload = await UpdateDocLineHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("DOC_NOT_DRAFT", payload.Error);
        Assert.Single(harness.GetDocLines(1));
        Assert.Single(harness.GetAllDocLines(1));
        Assert.Empty(harness.LedgerEntries);
    }

    private static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, string DocUid) CreateClosedScenarioWithLine()
    {
        const string docUid = "line-update-closed-001";
        var harness = new CloseDocumentHarness();
        harness.SeedItem(new Item
        {
            Id = 100,
            Name = "Mustard",
            Barcode = "4660011933641"
        });
        harness.SeedLocation(new Location
        {
            Id = 10,
            Code = "A1",
            Name = "Zone A1"
        });
        harness.SeedDoc(new Doc
        {
            Id = 1,
            DocRef = "IN-LINE-UPDATE-CLOSED-001",
            Type = DocType.Inbound,
            Status = DocStatus.Closed,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc),
            ClosedAt = new DateTime(2026, 3, 10, 11, 0, 0, DateTimeKind.Utc)
        });
        harness.SeedLine(new DocLine
        {
            Id = 1,
            DocId = 1,
            ItemId = 100,
            Qty = 5,
            ToLocationId = 10,
            UomCode = "BOX"
        });

        var apiStore = new InMemoryApiDocStore();
        apiStore.AddApiDoc(
            docUid,
            docId: 1,
            status: "CLOSED",
            docType: "INBOUND",
            docRef: "IN-LINE-UPDATE-CLOSED-001",
            partnerId: null,
            fromLocationId: null,
            toLocationId: 10,
            fromHu: null,
            toHu: null,
            deviceId: "API-01");

        return (harness, apiStore, docUid);
    }
}
