using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.CreateOrder.Infrastructure;

namespace FlowStock.Server.Tests.CreateOrder;

[Collection("CreateOrder")]
public sealed class LineNormalizationTests
{
    [Fact]
    public async Task DuplicateItemLines_NormalizeIntoOnePersistedLinePerItem()
    {
        var (harness, apiStore) = CreateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var payload = await CreateOrderHttpApi.CreateAsync(
            host.Client,
            new CreateOrderHttpApi.CreateOrderRequest
            {
                Type = "CUSTOMER",
                PartnerId = 200,
                Status = "DRAFT",
                Lines =
                [
                    new CreateOrderHttpApi.CreateOrderLineRequest { ItemId = 1001, QtyOrdered = 12 },
                    new CreateOrderHttpApi.CreateOrderLineRequest { ItemId = 1001, QtyOrdered = 3 },
                    new CreateOrderHttpApi.CreateOrderLineRequest { ItemId = 1002, QtyOrdered = 5 }
                ]
            });

        Assert.True(payload.Ok);
        var lines = harness.GetOrderLines(payload.OrderId);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, line => line.ItemId == 1001 && Math.Abs(line.QtyOrdered - 15) < 0.000001);
        Assert.Contains(lines, line => line.ItemId == 1002 && Math.Abs(line.QtyOrdered - 5) < 0.000001);
    }
}
