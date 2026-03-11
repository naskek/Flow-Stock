using System.Text.Json;
using FlowStock.Core.Models;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;
using FlowStock.Server.Tests.CreateOrder.Infrastructure;
using FlowStock.Server.Tests.SetOrderStatus.Infrastructure;

namespace FlowStock.Server.Tests.IncomingRequestsOrderConvergence.Infrastructure;

internal static class IncomingRequestsOrderConvergenceScenario
{
    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, OrderRequest Request) CreateCreateOrderApprovalScenario()
    {
        var (harness, apiStore) = CreateOrderHttpScenario.CreateCustomerScenario();
        var request = new OrderRequest
        {
            Id = 9001,
            RequestType = OrderRequestType.CreateOrder,
            PayloadJson = JsonSerializer.Serialize(new
            {
                order_ref = "IR-001",
                partner_id = 200,
                due_date = "2026-03-20",
                comment = "Из входящей заявки",
                lines = new[]
                {
                    new { item_id = 1001, qty_ordered = 12d },
                    new { item_id = 1002, qty_ordered = 4d }
                }
            }),
            Status = OrderRequestStatus.Pending,
            CreatedAt = new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc),
            CreatedByLogin = "web-user",
            CreatedByDeviceId = "WEB-01"
        };
        harness.SeedOrderRequest(request);
        return (harness, apiStore, request);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, OrderRequest Request) CreateInvalidCreateOrderApprovalScenario()
    {
        var (harness, apiStore) = CreateOrderHttpScenario.CreateCustomerScenario();
        var request = new OrderRequest
        {
            Id = 9002,
            RequestType = OrderRequestType.CreateOrder,
            PayloadJson = JsonSerializer.Serialize(new
            {
                order_ref = "IR-002",
                partner_id = 999,
                due_date = "2026-03-20",
                comment = "Некорректный контрагент",
                lines = new[]
                {
                    new { item_id = 1001, qty_ordered = 3d }
                }
            }),
            Status = OrderRequestStatus.Pending,
            CreatedAt = new DateTime(2026, 3, 11, 9, 5, 0, DateTimeKind.Utc),
            CreatedByLogin = "web-user",
            CreatedByDeviceId = "WEB-01"
        };
        harness.SeedOrderRequest(request);
        return (harness, apiStore, request);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, OrderRequest Request, long OrderId) CreateSetStatusApprovalScenario()
    {
        var (harness, apiStore, orderId) = SetOrderStatusHttpScenario.CreateDraftCustomerScenario();
        var request = new OrderRequest
        {
            Id = 9003,
            RequestType = OrderRequestType.SetOrderStatus,
            PayloadJson = JsonSerializer.Serialize(new
            {
                order_id = orderId,
                status = "ACCEPTED"
            }),
            Status = OrderRequestStatus.Pending,
            CreatedAt = new DateTime(2026, 3, 11, 9, 10, 0, DateTimeKind.Utc),
            CreatedByLogin = "web-user",
            CreatedByDeviceId = "WEB-01"
        };
        harness.SeedOrderRequest(request);
        return (harness, apiStore, request, orderId);
    }
}
