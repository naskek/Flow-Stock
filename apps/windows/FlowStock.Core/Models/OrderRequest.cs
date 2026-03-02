namespace FlowStock.Core.Models;

public static class OrderRequestType
{
    public const string CreateOrder = "CREATE_ORDER";
    public const string SetOrderStatus = "SET_ORDER_STATUS";
}

public static class OrderRequestStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

public sealed class OrderRequest
{
    public long Id { get; init; }
    public string RequestType { get; init; } = OrderRequestType.CreateOrder;
    public string PayloadJson { get; init; } = "{}";
    public string Status { get; init; } = OrderRequestStatus.Pending;
    public DateTime CreatedAt { get; init; }
    public string? CreatedByLogin { get; init; }
    public string? CreatedByDeviceId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public string? ResolutionNote { get; init; }
    public long? AppliedOrderId { get; init; }
}
