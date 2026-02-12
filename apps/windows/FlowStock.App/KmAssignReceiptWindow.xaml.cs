using System.Globalization;
using System.Windows;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class KmAssignReceiptWindow : Window
{
    private readonly AppServices _services;
    private readonly Doc _doc;
    private readonly DocLine _line;
    private readonly Item _item;
    private readonly List<BatchOption> _batches = new();

    public KmAssignReceiptWindow(AppServices services, Doc doc, DocLine line, Item item)
    {
        _services = services;
        _doc = doc;
        _line = line;
        _item = item;
        InitializeComponent();

        BatchCombo.ItemsSource = _batches;
        FillFromOrderButton.IsEnabled = _doc.OrderId.HasValue;
        LoadBatches();
        UpdateHeader();
    }

    private void LoadBatches()
    {
        _batches.Clear();
        foreach (var batch in _services.Km.GetBatches())
        {
            _batches.Add(new BatchOption(batch));
        }

        BatchCombo.SelectedItem = _batches.FirstOrDefault(option =>
                                       _doc.OrderId.HasValue && option.OrderId == _doc.OrderId.Value)
                                   ?? _batches.FirstOrDefault();
    }

    private void UpdateHeader()
    {
        ItemText.Text = _item.Name;
        CountText.Text = $"Нужно: {FormatQty(_line.Qty)} | Привязано: {GetAssignedCount()}";
    }

    private int GetAssignedCount()
    {
        return _services.Km.GetAssignedCountForReceiptLine(_line.Id);
    }

    private void FillFromBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_line.ToLocationId == null)
        {
            MessageBox.Show("Выберите локацию приемки.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var batchId = (BatchCombo.SelectedItem as BatchOption)?.Id;
        if (!batchId.HasValue)
        {
            MessageBox.Show("Выберите пакет.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AssignCodes(batchId.Value, null);
    }

    private void FillFromOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!_doc.OrderId.HasValue)
        {
            MessageBox.Show("Документ не связан с заказом.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AssignCodes(null, _doc.OrderId.Value);
    }

    private void AssignCodes(long? batchId, long? orderId)
    {
        try
        {
            var assigned = _services.Km.AssignCodesToReceipt(_doc.Id, _line, _item, batchId, orderId);
            ResultText.Text = $"Привязано кодов: {assigned}.";
            UpdateHeader();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Маркировка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatQty(double value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private sealed record BatchOption
    {
        public BatchOption(KmCodeBatch batch)
        {
            Id = batch.Id;
            OrderId = batch.OrderId;
            DisplayName = BuildDisplay(batch);
        }

        public long Id { get; }
        public long? OrderId { get; }
        public string DisplayName { get; }

        private static string BuildDisplay(KmCodeBatch batch)
        {
            var orderRef = string.IsNullOrWhiteSpace(batch.OrderRef) ? "-" : batch.OrderRef;
            var date = batch.ImportedAt.ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);
            return $"{date} | {orderRef} | {batch.FileName}";
        }
    }
}
