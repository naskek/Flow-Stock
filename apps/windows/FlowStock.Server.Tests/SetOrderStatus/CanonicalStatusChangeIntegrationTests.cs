using FlowStock.Core.Models;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.SetOrderStatus.Infrastructure;

namespace FlowStock.Server.Tests.SetOrderStatus;

[Collection("SetOrderStatus")]
public sealed class CanonicalStatusChangeIntegrationTests
{
    [Fact]
    public async Task SuccessfulStatusChangeForAllowedTransition()
    {
        var (harness, apiStore, orderId) = SetOrderStatusHttpScenario.CreateDraftCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var payload = await SetOrderStatusHttpApi.ChangeAsync(host.Client, orderId, "ACCEPTED");

        Assert.True(payload.Ok);
        Assert.Equal("STATUS_CHANGED", payload.Result);
        Assert.Equal(orderId, payload.OrderId);
        Assert.Equal("ACCEPTED", payload.Status);
        Assert.Equal(OrderStatus.Accepted, harness.GetOrder(orderId).Status);
    }
}
