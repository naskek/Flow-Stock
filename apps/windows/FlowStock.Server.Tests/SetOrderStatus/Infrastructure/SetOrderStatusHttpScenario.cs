using FlowStock.Core.Models;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;

namespace FlowStock.Server.Tests.SetOrderStatus.Infrastructure;

internal static class SetOrderStatusHttpScenario
{
    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateDraftCustomerScenario()
    {
        var harness = new CloseDocumentHarness();
        harness.SeedPartner(new Partner
        {
            Id = 200,
            Code = "CUST-200",
            Name = "Тестовый покупатель",
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });

        const long orderId = 30;
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "030",
            Type = OrderType.Customer,
            PartnerId = 200,
            Status = OrderStatus.Draft,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });
        harness.SeedOrderLine(new OrderLine { Id = 501, OrderId = orderId, ItemId = 1001, QtyOrdered = 10 });

        return (harness, new InMemoryApiDocStore(), orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateShippedCustomerScenario()
    {
        var (harness, apiStore, orderId) = CreateDraftCustomerScenario();
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "030",
            Type = OrderType.Customer,
            PartnerId = 200,
            Status = OrderStatus.Shipped,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc),
            ShippedAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc)
        });

        return (harness, apiStore, orderId);
    }
}
