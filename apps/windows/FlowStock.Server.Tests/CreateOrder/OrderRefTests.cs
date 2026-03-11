using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.CreateOrder.Infrastructure;

namespace FlowStock.Server.Tests.CreateOrder;

[Collection("CreateOrder")]
public sealed class OrderRefTests
{
    [Fact]
    public async Task MissingOrderRef_GeneratesServerOrderRef()
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
                    new CreateOrderHttpApi.CreateOrderLineRequest { ItemId = 1001, QtyOrdered = 12 }
                ]
            });

        Assert.True(payload.Ok);
        Assert.True(payload.OrderId > 0);
        Assert.Matches(@"^\d{3}$", payload.OrderRef ?? string.Empty);
        Assert.False(payload.OrderRefChanged);
        Assert.Equal(payload.OrderRef, harness.GetOrder(payload.OrderId).OrderRef);
    }

    [Fact]
    public async Task CollidingRequestedOrderRef_ReturnsReplacementWithOrderRefChanged()
    {
        const string requestedOrderRef = "001";

        var (harness, apiStore) = CreateOrderHttpScenario.CreateOrderRefCollisionScenario(requestedOrderRef);
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var payload = await CreateOrderHttpApi.CreateAsync(
            host.Client,
            new CreateOrderHttpApi.CreateOrderRequest
            {
                OrderRef = requestedOrderRef,
                Type = "CUSTOMER",
                PartnerId = 200,
                Status = "DRAFT",
                Lines =
                [
                    new CreateOrderHttpApi.CreateOrderLineRequest { ItemId = 1001, QtyOrdered = 12 }
                ]
            });

        Assert.True(payload.Ok);
        Assert.True(payload.OrderRefChanged);
        Assert.NotEqual(requestedOrderRef, payload.OrderRef);
        Assert.Matches(@"^\d{3}$", payload.OrderRef ?? string.Empty);
        Assert.Equal(payload.OrderRef, harness.GetOrder(payload.OrderId).OrderRef);
    }
}
