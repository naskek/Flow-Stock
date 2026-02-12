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
    private readonly List<string> _selectedFiles = new();

    public KmImportWindow(AppServices services, Action? onImported)
    {
        _services = services;
        _onImported = onImported;
        InitializeComponent();
        LoadOrders();
        UpdateSelectedFilesText();
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
            Filter = "CSV/TSV files (*.csv;*.tsv)|*.csv;*.tsv|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFiles.Clear();
            _selectedFiles.AddRange(dialog.FileNames.Where(path => !string.IsNullOrWhiteSpace(path)));
            UpdateSelectedFilesText();
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!TryResolveOrder(out var orderId))
        {
            return;
        }

        if (_selectedFiles.Count == 0)
        {
            MessageBox.Show("Файл не найден.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetImportState(true);
        ImportProgressBar.Maximum = _selectedFiles.Count;
        ImportProgressBar.Value = 0;
        ImportProgressBar.IsIndeterminate = _selectedFiles.Count == 1;
        ResultText.Text = string.Empty;

        var imported = 0;
        var duplicates = 0;
        var errors = 0;
        var invalidGtins = 0;
        var emptyCodes = 0;
        var unmatchedSku = 0;
        var duplicateFiles = 0;

        try
        {
            for (var i = 0; i < _selectedFiles.Count; i++)
            {
                var filePath = _selectedFiles[i];
                var fileName = Path.GetFileName(filePath);
                ImportStatusText.Text = $"Импорт: {i + 1} / {_selectedFiles.Count} · {fileName}";
                if (!File.Exists(filePath))
                {
                    errors++;
                    ImportProgressBar.Value = i + 1;
                    continue;
                }

                KmImportResult result;
                try
                {
                    result = await Task.Run(() => _services.Km.ImportCodes(filePath, orderId, Environment.UserName));
                }
                catch
                {
                    errors++;
                    ImportProgressBar.Value = i + 1;
                    continue;
                }

                if (result.IsDuplicateFile)
                {
                    duplicateFiles++;
                    ImportProgressBar.Value = i + 1;
                    continue;
                }

                imported += result.Imported;
                duplicates += result.Duplicates;
                errors += result.Errors;
                invalidGtins += result.InvalidGtins;
                emptyCodes += result.EmptyCodes;
                unmatchedSku += result.UnmatchedSku;
                ImportProgressBar.Value = i + 1;
            }

            ImportStatusText.Text = "Импорт завершен.";
            ResultText.Text = $"Импорт завершен.\nИмпортировано: {imported}\nДубли: {duplicates}\nОшибки: {errors}\nНекорректный GTIN: {invalidGtins}\nПустые коды: {emptyCodes}\nНе сопоставлено SKU: {unmatchedSku}\nПовторные файлы: {duplicateFiles}";
            _onImported?.Invoke();
        }
        finally
        {
            ImportProgressBar.IsIndeterminate = false;
            SetImportState(false);
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

    private void SetImportState(bool isImporting)
    {
        ImportButton.IsEnabled = !isImporting;
        OrderCombo.IsEnabled = !isImporting;
        FilePathBox.IsEnabled = !isImporting;
        BrowseButton.IsEnabled = !isImporting;
    }

    private void UpdateSelectedFilesText()
    {
        if (_selectedFiles.Count == 0)
        {
            FilePathBox.Text = string.Empty;
            SelectedFilesText.Text = "Файлы не выбраны.";
            return;
        }

        if (_selectedFiles.Count == 1)
        {
            FilePathBox.Text = _selectedFiles[0];
            SelectedFilesText.Text = "Выбран 1 файл.";
            return;
        }

        FilePathBox.Text = $"Выбрано файлов: {_selectedFiles.Count}";
        SelectedFilesText.Text = string.Join("\n", _selectedFiles.Select(Path.GetFileName));
    }

    private sealed record OrderOption(long Id, string OrderRef, string PartnerDisplay)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(PartnerDisplay) ? OrderRef : $"{OrderRef} - {PartnerDisplay}";
    }
}
