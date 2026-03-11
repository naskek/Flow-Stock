using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.CreateOrder.Infrastructure;

namespace FlowStock.Server.Tests.CreateOrder;

[Collection("CreateOrder")]
public sealed class StateGuaranteeTests
{
    [Fact]
    public async Task Create_DoesNotWriteDocsOrLedger()
    {
        var (harness, apiStore) = CreateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        await CreateOrderHttpApi.CreateAsync(
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

        Assert.Equal(0, harness.DocCount);
        Assert.Empty(harness.LedgerEntries);
    }
}
