using FlowStock.Core.Models;
using FlowStock.Server.Tests.CloseDocument.Infrastructure;

namespace FlowStock.Server.Tests.DeleteOrder.Infrastructure;

internal static class DeleteOrderHttpScenario
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
        harness.SeedItem(new Item { Id = 1001, Name = "Горчица", Barcode = "4660011933641" });
        harness.SeedItem(new Item { Id = 1002, Name = "Кетчуп", Barcode = "4660011933642" });

        const long orderId = 20;
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "020",
            Type = OrderType.Customer,
            PartnerId = 200,
            Status = OrderStatus.Draft,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });
        harness.SeedOrderLine(new OrderLine { Id = 301, OrderId = orderId, ItemId = 1001, QtyOrdered = 10 });
        harness.SeedOrderLine(new OrderLine { Id = 302, OrderId = orderId, ItemId = 1002, QtyOrdered = 5 });

        return (harness, new InMemoryApiDocStore(), orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateAcceptedCustomerScenario()
    {
        var (harness, apiStore, orderId) = CreateDraftCustomerScenario();
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "020",
            Type = OrderType.Customer,
            PartnerId = 200,
            Status = OrderStatus.Accepted,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });
        return (harness, apiStore, orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateCustomerWithOutboundDocsScenario()
    {
        var (harness, apiStore, orderId) = CreateDraftCustomerScenario();
        harness.SeedHasOutboundDocs(orderId);
        return (harness, apiStore, orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateCustomerWithShipmentsScenario()
    {
        var (harness, apiStore, orderId) = CreateDraftCustomerScenario();
        harness.SeedShippedTotalsByOrderLine(orderId, new Dictionary<long, double>
        {
            [301] = 1
        });
        return (harness, apiStore, orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateInternalWithProductionDocsScenario()
    {
        var harness = new CloseDocumentHarness();
        harness.SeedItem(new Item { Id = 1001, Name = "Горчица", Barcode = "4660011933641" });

        const long orderId = 21;
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "021",
            Type = OrderType.Internal,
            Status = OrderStatus.Draft,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });
        harness.SeedOrderLine(new OrderLine { Id = 401, OrderId = orderId, ItemId = 1001, QtyOrdered = 6 });
        harness.SeedDoc(new Doc
        {
            Id = 900,
            DocRef = "PRD-2026-000001",
            Type = DocType.ProductionReceipt,
            Status = DocStatus.Draft,
            OrderId = orderId,
            CreatedAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc)
        });

        return (harness, new InMemoryApiDocStore(), orderId);
    }

    public static (CloseDocumentHarness Harness, InMemoryApiDocStore ApiStore, long OrderId) CreateInternalWithReceiptsScenario()
    {
        var harness = new CloseDocumentHarness();
        harness.SeedItem(new Item { Id = 1001, Name = "Горчица", Barcode = "4660011933641" });

        const long orderId = 22;
        harness.SeedOrder(new Order
        {
            Id = orderId,
            OrderRef = "022",
            Type = OrderType.Internal,
            Status = OrderStatus.Draft,
            CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
        });
        harness.SeedOrderLine(new OrderLine { Id = 402, OrderId = orderId, ItemId = 1001, QtyOrdered = 6 });
        harness.SeedOrderReceiptRemaining(
            orderId,
            new OrderReceiptLine
            {
                OrderLineId = 402,
                ItemId = 1001,
                ItemName = "Горчица",
                QtyOrdered = 6,
                QtyReceived = 2
            });

        return (harness, new InMemoryApiDocStore(), orderId);
    }
}
