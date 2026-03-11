using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.DeleteOrder.Infrastructure;

namespace FlowStock.Server.Tests.DeleteOrder;

[Collection("DeleteOrder")]
public sealed class StateGuaranteeTests
{
    [Fact]
    public async Task Delete_DoesNotWriteDocsOrLedger()
    {
        var (harness, apiStore, orderId) = DeleteOrderHttpScenario.CreateDraftCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var docsBefore = harness.DocCount;
        var ledgerBefore = harness.LedgerEntries.Count;

        await DeleteOrderHttpApi.DeleteAsync(host.Client, orderId);

        Assert.Equal(docsBefore, harness.DocCount);
        Assert.Equal(ledgerBefore, harness.LedgerEntries.Count);
    }
}
