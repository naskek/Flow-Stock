using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.DeleteOrder.Infrastructure;

namespace FlowStock.Server.Tests.DeleteOrder;

[Collection("DeleteOrder")]
public sealed class CanonicalDeleteIntegrationTests
{
    [Fact]
    public async Task SuccessfulDeleteOfAllowedOrder()
    {
        var (harness, apiStore, orderId) = DeleteOrderHttpScenario.CreateDraftCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var payload = await DeleteOrderHttpApi.DeleteAsync(host.Client, orderId);

        Assert.True(payload.Ok);
        Assert.Equal("DELETED", payload.Result);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal("020", payload.OrderRef);
        Assert.Null(harness.Store.GetOrder(orderId));
        Assert.Empty(harness.GetOrderLines(orderId));
        Assert.Equal(0, harness.OrderCount);
    }
}
