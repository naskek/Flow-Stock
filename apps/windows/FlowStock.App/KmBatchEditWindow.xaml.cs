using System.Globalization;
using System.Windows;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class KmBatchEditWindow : Window
{
    private readonly AppServices _services;
    private readonly KmCodeBatch _batch;
    private readonly Action? _onSaved;
    private readonly List<OrderOption> _orders = new();

    public KmBatchEditWindow(AppServices services, KmCodeBatch batch, Action? onSaved)
    {
        _services = services;
        _batch = batch;
        _onSaved = onSaved;
        InitializeComponent();
        LoadOrders();
        FillBatchInfo();
    }

    private void LoadOrders()
    {
        _orders.Clear();
        _orders.Add(OrderOption.Empty);
        foreach (var order in _services.Orders.GetOrders())
        {
            _orders.Add(new OrderOption(order.Id, order.OrderRef, order.PartnerDisplay));
        }

        OrderCombo.ItemsSource = _orders;
        OrderCombo.SelectedItem = _orders.FirstOrDefault(option => option.Id == _batch.OrderId) ?? OrderOption.Empty;
    }

    private void FillBatchInfo()
    {
        var date = _batch.ImportedAt.ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);
        var orderRef = string.IsNullOrWhiteSpace(_batch.OrderRef) ? "-" : _batch.OrderRef;
        BatchInfoText.Text = $"Пакет: {_batch.FileName}\nДата: {date}\nЗаказ: {orderRef}";
        HintText.Text = "Оставьте пустым, чтобы убрать привязку.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryResolveOrder(out var orderId))
        {
            return;
        }

        try
        {
            _services.Km.UpdateBatchOrder(_batch.Id, orderId);
            _onSaved?.Invoke();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Маркировка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryResolveOrder(out long? orderId)
    {
        orderId = null;
        if (OrderCombo.SelectedItem is OrderOption selected)
        {
            orderId = selected.Id;
            return true;
        }

        var text = OrderCombo.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var match = _orders.FirstOrDefault(order => string.Equals(order.OrderRef, text, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            MessageBox.Show("Заказ не найден. Выберите заказ из списка.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        OrderCombo.SelectedItem = match;
        orderId = match.Id;
        return true;
    }

    private sealed record OrderOption(long? Id, string OrderRef, string PartnerDisplay)
    {
        public static OrderOption Empty { get; } = new(null, string.Empty, string.Empty);

        public string DisplayName => Id.HasValue
            ? (string.IsNullOrWhiteSpace(PartnerDisplay) ? OrderRef : $"{OrderRef} - {PartnerDisplay}")
            : "Без заказа";
    }
}
