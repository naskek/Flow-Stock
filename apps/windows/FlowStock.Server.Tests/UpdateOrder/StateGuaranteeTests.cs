using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.UpdateOrder.Infrastructure;

namespace FlowStock.Server.Tests.UpdateOrder;

[Collection("UpdateOrder")]
public sealed class StateGuaranteeTests
{
    [Fact]
    public async Task Update_DoesNotWriteDocsOrLedger()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var docsBefore = harness.DocCount;
        var ledgerBefore = harness.LedgerEntries.Count;

        await UpdateOrderHttpApi.UpdateAsync(
            host.Client,
            orderId,
            new UpdateOrderHttpApi.UpdateOrderRequest
            {
                OrderRef = "002",
                Type = "CUSTOMER",
                PartnerId = 200,
                Status = "ACCEPTED",
                Lines =
                [
                    new UpdateOrderHttpApi.UpdateOrderLineRequest { ItemId = 1001, QtyOrdered = 10 }
                ]
            });

        Assert.Equal(docsBefore, harness.DocCount);
        Assert.Equal(ledgerBefore, harness.LedgerEntries.Count);
    }
}
