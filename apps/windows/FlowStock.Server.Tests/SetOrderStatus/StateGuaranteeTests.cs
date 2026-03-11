using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.SetOrderStatus.Infrastructure;

namespace FlowStock.Server.Tests.SetOrderStatus;

[Collection("SetOrderStatus")]
public sealed class StateGuaranteeTests
{
    [Fact]
    public async Task StatusChange_DoesNotWriteDocsOrLedger()
    {
        var (harness, apiStore, orderId) = SetOrderStatusHttpScenario.CreateDraftCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        var docsBefore = harness.DocCount;
        var ledgerBefore = harness.LedgerEntries.Count;

        await SetOrderStatusHttpApi.ChangeAsync(host.Client, orderId, "IN_PROGRESS");

        Assert.Equal(docsBefore, harness.DocCount);
        Assert.Equal(ledgerBefore, harness.LedgerEntries.Count);
    }
}
