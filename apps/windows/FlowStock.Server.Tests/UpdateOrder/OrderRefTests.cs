using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.UpdateOrder.Infrastructure;

namespace FlowStock.Server.Tests.UpdateOrder;

[Collection("UpdateOrder")]
public sealed class OrderRefTests
{
    [Fact]
    public async Task CollidingRequestedOrderRef_ReturnsReplacementWithOrderRefChanged()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateOrderRefCollisionScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var payload = await UpdateOrderHttpApi.UpdateAsync(
            host.Client,
            orderId,
            new UpdateOrderHttpApi.UpdateOrderRequest
            {
                OrderRef = "777",
                Type = "CUSTOMER",
                PartnerId = 200,
                Status = "ACCEPTED",
                Lines =
                [
                    new UpdateOrderHttpApi.UpdateOrderLineRequest { ItemId = 1001, QtyOrdered = 10 }
                ]
            });

        Assert.True(payload.Ok);
        Assert.True(payload.OrderRefChanged);
        Assert.NotEqual("777", payload.OrderRef);
        Assert.Equal(payload.OrderRef, harness.GetOrder(orderId).OrderRef);
    }
}
