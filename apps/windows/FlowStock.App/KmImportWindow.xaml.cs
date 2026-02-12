using System.IO;
using System.Windows;
using FlowStock.Core.Models;
using Microsoft.Win32;

namespace FlowStock.App;

public partial class KmImportWindow : Window
{
    private readonly AppServices _services;
    private readonly Action? _onImported;
    private readonly List<OrderOption> _orders = new();

    public KmImportWindow(AppServices services, Action? onImported)
    {
        _services = services;
        _onImported = onImported;
        InitializeComponent();
        LoadOrders();
    }

    private void LoadOrders()
    {
        _orders.Clear();
        foreach (var order in _services.Orders.GetOrders())
        {
            _orders.Add(new OrderOption(order.Id, order.OrderRef, order.PartnerDisplay));
        }

        OrderCombo.ItemsSource = _orders;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV/TSV files (*.csv;*.tsv)|*.csv;*.tsv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePathBox.Text = dialog.FileName;
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!TryResolveOrder(out var orderId))
        {
            return;
        }

        var path = FilePathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show("Файл не найден.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = _services.Km.ImportCodes(path!, orderId, Environment.UserName);
        if (result.IsDuplicateFile)
        {
            MessageBox.Show("Этот файл уже был импортирован.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResultText.Text = $"Импорт завершен.\nИмпортировано: {result.Imported}\nДубли: {result.Duplicates}\nОшибки: {result.Errors}\nНекорректный GTIN: {result.InvalidGtins}\nПустые коды: {result.EmptyCodes}\nНе сопоставлено SKU: {result.UnmatchedSku}";
        _onImported?.Invoke();
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

    private sealed record OrderOption(long Id, string OrderRef, string PartnerDisplay)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(PartnerDisplay) ? OrderRef : $"{OrderRef} - {PartnerDisplay}";
    }
}
