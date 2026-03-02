using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class OrderRequestsWindow : Window
{
    private readonly AppServices _services;
    private readonly ObservableCollection<OrderRequestRow> _rows = new();
    private readonly Action? _onChanged;

    public OrderRequestsWindow(AppServices services, Action? onChanged)
    {
        _services = services;
        _onChanged = onChanged;
        InitializeComponent();

        RequestsGrid.ItemsSource = _rows;
        RequestsGrid.SelectionChanged += RequestsGrid_SelectionChanged;
        LoadRequests();
    }

    private void LoadRequests()
    {
        _rows.Clear();
        var includeResolved = ShowResolvedCheck?.IsChecked == true;
        foreach (var request in _services.DataStore.GetOrderRequests(includeResolved))
        {
            _rows.Add(new OrderRequestRow
            {
                Request = request,
                TypeDisplay = GetTypeDisplay(request.RequestType),
                Summary = BuildSummary(request),
                RequestedBy = BuildRequestedBy(request),
                StatusDisplay = GetStatusDisplay(request.Status),
                CreatedAt = request.CreatedAt,
                ResolvedAt = request.ResolvedAt,
                ResolutionNote = request.ResolutionNote
            });
        }

        UpdateButtons();
    }

    private void RequestsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasPendingSelection = GetSelectedPending().Count > 0;
        ApproveButton.IsEnabled = hasPendingSelection;
        RejectButton.IsEnabled = hasPendingSelection;
    }

    private List<OrderRequestRow> GetSelectedPending()
    {
        return RequestsGrid.SelectedItems
            .Cast<OrderRequestRow>()
            .Where(row => string.Equals(row.Request.Status, OrderRequestStatus.Pending, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void Approve_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedPending();
        if (selected.Count == 0)
        {
            MessageBox.Show("Выберите необработанные заявки.", "Заявки", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Подтвердить выбранные заявки ({selected.Count})?",
            "Заявки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var resolvedBy = Environment.UserName;
        var errors = new List<string>();
        var approved = 0;

        foreach (var row in selected)
        {
            try
            {
                var outcome = ApplyRequest(row.Request);
                _services.DataStore.ResolveOrderRequest(
                    row.Request.Id,
                    OrderRequestStatus.Approved,
                    resolvedBy,
                    outcome.Note,
                    outcome.AppliedOrderId);
                approved++;
            }
            catch (Exception ex)
            {
                errors.Add($"#{row.Request.Id}: {ex.Message}");
            }
        }

        LoadRequests();
        _onChanged?.Invoke();

        if (errors.Count > 0)
        {
            var text = "Часть заявок не удалось применить:\n" + string.Join("\n", errors);
            MessageBox.Show(text, "Заявки", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (approved > 0)
        {
            MessageBox.Show($"Подтверждено: {approved}.", "Заявки", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Reject_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedPending();
        if (selected.Count == 0)
        {
            MessageBox.Show("Выберите необработанные заявки.", "Заявки", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Отклонить выбранные заявки ({selected.Count})?",
            "Заявки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var resolvedBy = Environment.UserName;
        foreach (var row in selected)
        {
            _services.DataStore.ResolveOrderRequest(
                row.Request.Id,
                OrderRequestStatus.Rejected,
                resolvedBy,
                "Отклонено оператором WPF.",
                null);
        }

        LoadRequests();
        _onChanged?.Invoke();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadRequests();
    }

    private void ShowResolvedCheck_Changed(object sender, RoutedEventArgs e)
    {
        LoadRequests();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private RequestApplyResult ApplyRequest(OrderRequest request)
    {
        if (string.Equals(request.RequestType, OrderRequestType.CreateOrder, StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Deserialize<CreateOrderPayload>(request.PayloadJson, JsonOptions)
                          ?? throw new InvalidOperationException("Некорректный payload заявки CREATE_ORDER.");
            if (string.IsNullOrWhiteSpace(payload.OrderRef))
            {
                throw new InvalidOperationException("Не указан номер заказа.");
            }

            if (payload.PartnerId <= 0)
            {
                throw new InvalidOperationException("Не указан контрагент.");
            }

            var dueDate = ParseDueDate(payload.DueDate);
            var lines = payload.Lines?
                .Where(line => line.ItemId > 0 && line.QtyOrdered > 0)
                .Select(line => new OrderLineView
                {
                    ItemId = line.ItemId,
                    QtyOrdered = line.QtyOrdered
                })
                .ToList() ?? new List<OrderLineView>();
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("В заявке нет строк заказа.");
            }

            var createdOrderId = _services.Orders.CreateOrder(
                payload.OrderRef.Trim(),
                payload.PartnerId,
                dueDate,
                OrderStatus.Accepted,
                payload.Comment,
                lines);
            return new RequestApplyResult(createdOrderId, $"Создан заказ ID={createdOrderId}.");
        }

        if (string.Equals(request.RequestType, OrderRequestType.SetOrderStatus, StringComparison.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Deserialize<SetOrderStatusPayload>(request.PayloadJson, JsonOptions)
                          ?? throw new InvalidOperationException("Некорректный payload заявки SET_ORDER_STATUS.");
            if (payload.OrderId <= 0)
            {
                throw new InvalidOperationException("Не указан заказ для смены статуса.");
            }

            var nextStatus = OrderStatusMapper.StatusFromString(payload.Status)
                             ?? throw new InvalidOperationException("Неизвестный статус в заявке.");
            _services.Orders.ChangeOrderStatus(payload.OrderId, nextStatus);
            var statusDisplay = OrderStatusMapper.StatusToDisplayName(nextStatus);
            return new RequestApplyResult(payload.OrderId, $"Статус изменен на \"{statusDisplay}\".");
        }

        throw new InvalidOperationException($"Неизвестный тип заявки: {request.RequestType}");
    }

    private static DateTime? ParseDueDate(string? dueDate)
    {
        if (string.IsNullOrWhiteSpace(dueDate))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                dueDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return parsed.Date;
        }

        throw new InvalidOperationException("Некорректная дата отгрузки в заявке.");
    }

    private static string BuildSummary(OrderRequest request)
    {
        try
        {
            using var doc = JsonDocument.Parse(request.PayloadJson);
            var root = doc.RootElement;
            if (string.Equals(request.RequestType, OrderRequestType.CreateOrder, StringComparison.OrdinalIgnoreCase))
            {
                var orderRef = root.TryGetProperty("order_ref", out var refEl) ? refEl.GetString() : null;
                var partnerId = root.TryGetProperty("partner_id", out var partnerEl) ? partnerEl.GetInt64() : 0;
                var lineCount = root.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array
                    ? linesEl.GetArrayLength()
                    : 0;
                return $"Создать заказ {orderRef ?? "-"} · контрагент ID={partnerId} · строк: {lineCount}";
            }

            if (string.Equals(request.RequestType, OrderRequestType.SetOrderStatus, StringComparison.OrdinalIgnoreCase))
            {
                var orderId = root.TryGetProperty("order_id", out var orderEl) ? orderEl.GetInt64() : 0;
                var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                var displayStatus = OrderStatusMapper.StatusFromString(status) is { } parsed
                    ? OrderStatusMapper.StatusToDisplayName(parsed)
                    : status ?? "-";
                return $"Смена статуса · заказ ID={orderId} -> {displayStatus}";
            }
        }
        catch
        {
            // keep fallback summary
        }

        return request.PayloadJson;
    }

    private static string BuildRequestedBy(OrderRequest request)
    {
        var login = request.CreatedByLogin?.Trim();
        var device = request.CreatedByDeviceId?.Trim();

        if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(device))
        {
            return $"{login} ({device})";
        }

        if (!string.IsNullOrWhiteSpace(login))
        {
            return login;
        }

        if (!string.IsNullOrWhiteSpace(device))
        {
            return device;
        }

        return "-";
    }

    private static string GetTypeDisplay(string requestType)
    {
        if (string.Equals(requestType, OrderRequestType.CreateOrder, StringComparison.OrdinalIgnoreCase))
        {
            return "Создание заказа";
        }

        if (string.Equals(requestType, OrderRequestType.SetOrderStatus, StringComparison.OrdinalIgnoreCase))
        {
            return "Смена статуса";
        }

        return requestType;
    }

    private static string GetStatusDisplay(string status)
    {
        if (string.Equals(status, OrderRequestStatus.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return "Ожидает";
        }

        if (string.Equals(status, OrderRequestStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return "Подтвержден";
        }

        if (string.Equals(status, OrderRequestStatus.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return "Отклонен";
        }

        return status;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record RequestApplyResult(long? AppliedOrderId, string? Note);

    private sealed record CreateOrderPayload
    {
        [JsonPropertyName("order_ref")]
        public string? OrderRef { get; init; }

        [JsonPropertyName("partner_id")]
        public long PartnerId { get; init; }

        [JsonPropertyName("due_date")]
        public string? DueDate { get; init; }

        [JsonPropertyName("comment")]
        public string? Comment { get; init; }

        [JsonPropertyName("lines")]
        public List<CreateOrderLinePayload>? Lines { get; init; }
    }

    private sealed record CreateOrderLinePayload
    {
        [JsonPropertyName("item_id")]
        public long ItemId { get; init; }

        [JsonPropertyName("qty_ordered")]
        public double QtyOrdered { get; init; }
    }

    private sealed record SetOrderStatusPayload
    {
        [JsonPropertyName("order_id")]
        public long OrderId { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }
    }

    private sealed record OrderRequestRow
    {
        public required OrderRequest Request { get; init; }
        public required string TypeDisplay { get; init; }
        public required string Summary { get; init; }
        public required string RequestedBy { get; init; }
        public required string StatusDisplay { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public string? ResolutionNote { get; init; }
    }
}
