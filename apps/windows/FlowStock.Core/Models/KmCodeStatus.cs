namespace FlowStock.Core.Models;

public enum KmCodeStatus
{
    InPool = 0,
    OnHand = 1,
    Shipped = 2,
    Blocked = 3
}

public static class KmCodeStatusMapper
{
    public static KmCodeStatus FromInt(int value)
    {
        return value switch
        {
            1 => KmCodeStatus.OnHand,
            2 => KmCodeStatus.Shipped,
            3 => KmCodeStatus.Blocked,
            _ => KmCodeStatus.InPool
        };
    }

    public static int ToInt(KmCodeStatus status)
    {
        return (int)status;
    }

    public static string ToDisplayName(KmCodeStatus status)
    {
        return status switch
        {
            KmCodeStatus.InPool => "В пуле",
            KmCodeStatus.OnHand => "На складе",
            KmCodeStatus.Shipped => "Отгружен",
            KmCodeStatus.Blocked => "Заблокирован",
            _ => "Неизвестно"
        };
    }
}
