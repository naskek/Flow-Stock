using System.Net;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.UpdateOrder.Infrastructure;

namespace FlowStock.Server.Tests.UpdateOrder;

[Collection("UpdateOrder")]
public sealed class ValidationTests
{
    [Fact]
    public async Task UnknownOrderId_Fails()
    {
        var (harness, apiStore, _) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            999,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 200,
              "status": "DRAFT",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.NotFound);
        Assert.False(payload.Ok);
        Assert.Equal("ORDER_NOT_FOUND", payload.Error);
    }

    [Fact]
    public async Task MissingLines_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 200,
              "status": "DRAFT",
              "lines": []
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("MISSING_LINES", payload.Error);
    }

    [Fact]
    public async Task UnknownPartner_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 999,
              "status": "DRAFT",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("PARTNER_NOT_FOUND", payload.Error);
    }

    [Fact]
    public async Task SupplierPartner_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 201,
              "status": "DRAFT",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("PARTNER_IS_SUPPLIER", payload.Error);
    }

    [Fact]
    public async Task InvalidDueDate_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 200,
              "due_date": "25/03/2026",
              "status": "DRAFT",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("INVALID_DUE_DATE", payload.Error);
    }

    [Fact]
    public async Task ShippedStatus_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 200,
              "status": "SHIPPED",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("SHIPPED_STATUS_FORBIDDEN", payload.Error);
    }

    [Fact]
    public async Task ExistingShippedOrder_Fails()
    {
        var (harness, apiStore, orderId) = UpdateOrderHttpScenario.CreateShippedCustomerScenario();
        await using var host = await CloseDocumentHttpHost.StartAsync(harness, apiStore);

        using var response = await UpdateOrderHttpApi.PutRawAsync(
            host.Client,
            orderId,
            """
            {
              "order_ref": "002",
              "type": "CUSTOMER",
              "partner_id": 200,
              "status": "ACCEPTED",
              "lines": [{ "item_id": 1001, "qty_ordered": 10 }]
            }
            """);

        var payload = await UpdateOrderHttpApi.ReadApiResultAsync(response, HttpStatusCode.BadRequest);
        Assert.False(payload.Ok);
        Assert.Equal("ORDER_NOT_EDITABLE", payload.Error);
    }
}
