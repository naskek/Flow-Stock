using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LightWms.Core.Models;

namespace LightWms.App;

public partial class OperationDetailsWindow : Window
{
    private readonly AppServices _services;
    private readonly ObservableCollection<Item> _items = new();
    private readonly ObservableCollection<Location> _locations = new();
    private readonly ObservableCollection<Partner> _partners = new();
    private readonly ObservableCollection<DocLineView> _docLines = new();
    private readonly long _docId;
    private Doc? _doc;
    private DocLineView? _selectedDocLine;

    public OperationDetailsWindow(AppServices services, long docId)
    {
        _services = services;
        _docId = docId;
        InitializeComponent();

        DocLinesGrid.ItemsSource = _docLines;
        DocItemCombo.ItemsSource = _items;
        DocFromCombo.ItemsSource = _locations;
        DocToCombo.ItemsSource = _locations;
        DocPartnerCombo.ItemsSource = _partners;

        LoadCatalog();
        LoadDoc();
    }

    private void OperationDetailsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            e.Handled = true;
            TryCloseCurrentDoc();
        }
    }

    private void LoadCatalog()
    {
        _items.Clear();
        foreach (var item in _services.Catalog.GetItems(null))
        {
            _items.Add(item);
        }

        _locations.Clear();
        foreach (var location in _services.Catalog.GetLocations())
        {
            _locations.Add(location);
        }

        _partners.Clear();
        foreach (var partner in _services.Catalog.GetPartners())
        {
            _partners.Add(partner);
        }
    }

    private void LoadDoc()
    {
        _doc = _services.Documents.GetDoc(_docId);
        if (_doc == null)
        {
            MessageBox.Show("Операция не найдена.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }

        Title = $"Операция: {_doc.DocRef} ({DocTypeMapper.ToDisplayName(_doc.Type)})";
        LoadDocLines();
        UpdateDocView();
    }

    private void LoadDocLines()
    {
        _docLines.Clear();
        foreach (var line in _services.Documents.GetDocLines(_docId))
        {
            _docLines.Add(line);
        }

        _selectedDocLine = null;
        DocLineQtyBox.Text = string.Empty;
    }

    private void DocLines_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedDocLine = DocLinesGrid.SelectedItem as DocLineView;
        if (_selectedDocLine == null)
        {
            DocLineQtyBox.Text = string.Empty;
            return;
        }

        DocLineQtyBox.Text = _selectedDocLine.Qty.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private void DocLinesGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            DocDeleteLine_Click(sender, new RoutedEventArgs());
        }
    }

    private void DocBarcodeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            DocAddLine_Click(sender, new RoutedEventArgs());
        }
    }

    private void DocClose_Click(object sender, RoutedEventArgs e)
    {
        TryCloseCurrentDoc();
    }

    private void TryCloseCurrentDoc()
    {
        if (_doc == null)
        {
            MessageBox.Show("Операция не выбрана.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_doc.Status == DocStatus.Closed)
        {
            MessageBox.Show("Операция уже закрыта.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = _services.Documents.TryCloseDoc(_doc.Id, allowNegative: false);
        if (result.Errors.Count > 0)
        {
            MessageBox.Show(string.Join("\n", result.Errors), "Проверка операции", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (result.Warnings.Count > 0)
        {
            var warningText = "Остаток уйдет в минус:\n" + string.Join("\n", result.Warnings) + "\n\nЗакрыть операцию?";
            var confirm = MessageBox.Show(warningText, "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            result = _services.Documents.TryCloseDoc(_doc.Id, allowNegative: true);
            if (!result.Success)
            {
                if (result.Errors.Count > 0)
                {
                    MessageBox.Show(string.Join("\n", result.Errors), "Проверка операции", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }
        }

        if (!result.Success)
        {
            return;
        }

        LoadDoc();
    }

    private void DocAddLine_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureDraftDocSelected())
        {
            return;
        }

        if (!TryResolveDocItem(out var item))
        {
            return;
        }

        if (!TryParseQty(DocItemQtyBox.Text, out var qty))
        {
            MessageBox.Show("Количество должно быть больше 0.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fromLocation = DocFromCombo.SelectedItem as Location;
        var toLocation = DocToCombo.SelectedItem as Location;
        if (_doc!.Type == DocType.Inbound)
        {
            fromLocation = null;
        }
        else if (_doc.Type == DocType.WriteOff || _doc.Type == DocType.Outbound)
        {
            toLocation = null;
        }

        if (!ValidateLineLocations(_doc!, fromLocation, toLocation))
        {
            return;
        }

        try
        {
            _services.Documents.AddDocLine(_doc!.Id, item!.Id, qty, fromLocation?.Id, toLocation?.Id);
            DocItemQtyBox.Text = string.Empty;
            DocBarcodeBox.Text = string.Empty;
            LoadDocLines();
            DocBarcodeBox.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Операция", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DocUpdateLine_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureDraftDocSelected())
        {
            return;
        }

        if (_selectedDocLine == null)
        {
            MessageBox.Show("Выберите строку.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseQty(DocLineQtyBox.Text, out var qty))
        {
            MessageBox.Show("Количество должно быть больше 0.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _services.Documents.UpdateDocLineQty(_doc!.Id, _selectedDocLine.Id, qty);
            LoadDocLines();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Операция", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DocDeleteLine_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureDraftDocSelected())
        {
            return;
        }

        if (_selectedDocLine == null)
        {
            MessageBox.Show("Выберите строку.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _services.Documents.DeleteDocLine(_doc!.Id, _selectedDocLine.Id);
            LoadDocLines();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Операция", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DocHeaderSave_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null)
        {
            MessageBox.Show("Операция не выбрана.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_doc.Status != DocStatus.Draft)
        {
            MessageBox.Show("Операция уже закрыта.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var partnerId = (DocPartnerCombo.SelectedItem as Partner)?.Id;
        try
        {
            _services.Documents.UpdateDocHeader(_doc.Id, partnerId, DocOrderRefBox.Text, DocShippingRefBox.Text);
            LoadDoc();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Операция", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDocView()
    {
        if (_doc == null)
        {
            return;
        }

        DocInfoText.Text = FormatDocHeader(_doc);
        DocCloseButton.IsEnabled = _doc.Status != DocStatus.Closed;
        DocEditGroup.Visibility = _doc.Status == DocStatus.Draft ? Visibility.Visible : Visibility.Collapsed;

        var showFrom = _doc.Type != DocType.Inbound;
        var showTo = _doc.Type != DocType.WriteOff && _doc.Type != DocType.Outbound;
        DocFromLabel.Visibility = showFrom ? Visibility.Visible : Visibility.Collapsed;
        DocFromCombo.Visibility = showFrom ? Visibility.Visible : Visibility.Collapsed;
        DocToLabel.Visibility = showTo ? Visibility.Visible : Visibility.Collapsed;
        DocToCombo.Visibility = showTo ? Visibility.Visible : Visibility.Collapsed;

        if (!showFrom)
        {
            DocFromCombo.SelectedItem = null;
        }

        if (!showTo)
        {
            DocToCombo.SelectedItem = null;
        }

        DocHeaderPanel.IsEnabled = _doc.Status == DocStatus.Draft;
        DocPartnerCombo.SelectedItem = _partners.FirstOrDefault(p => p.Id == _doc.PartnerId);
        DocOrderRefBox.Text = _doc.OrderRef ?? string.Empty;
        DocShippingRefBox.Text = _doc.ShippingRef ?? string.Empty;

        if (_doc.Status == DocStatus.Draft)
        {
            DocBarcodeBox.Focus();
            DocBarcodeBox.SelectAll();
        }
    }

    private static string FormatDocHeader(Doc doc)
    {
        var createdAt = doc.CreatedAt.ToString("g");
        var closedAt = doc.ClosedAt.HasValue ? doc.ClosedAt.Value.ToString("g") : "-";
        return $"Номер: {doc.DocRef} | Тип: {DocTypeMapper.ToDisplayName(doc.Type)} | Статус: {DocTypeMapper.StatusToDisplayName(doc.Status)} | Создан: {createdAt} | Закрыт: {closedAt}";
    }

    private static bool TryParseQty(string input, out double qty)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out qty) && qty > 0;
    }

    private bool TryResolveDocItem(out Item? item)
    {
        item = null;
        var barcode = DocBarcodeBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            item = _items.FirstOrDefault(i => string.Equals(i.Barcode, barcode, StringComparison.OrdinalIgnoreCase))
                   ?? _items.FirstOrDefault(i => string.Equals(i.Gtin, barcode, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                MessageBox.Show("Товар со штрихкодом/GTIN не найден.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            DocItemCombo.SelectedItem = item;
            return true;
        }

        if (DocItemCombo.SelectedItem is Item selected)
        {
            item = selected;
            return true;
        }

        MessageBox.Show("Выберите товар или укажите штрихкод.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private bool EnsureDraftDocSelected()
    {
        if (_doc == null)
        {
            MessageBox.Show("Операция не выбрана.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (_doc.Status != DocStatus.Draft)
        {
            MessageBox.Show("Операция уже закрыта.", "Операция", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private bool ValidateLineLocations(Doc doc, Location? fromLocation, Location? toLocation)
    {
        switch (doc.Type)
        {
            case DocType.Inbound:
                if (toLocation == null)
                {
                    MessageBox.Show("Для приемки выберите место хранения получателя.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            case DocType.WriteOff:
                if (fromLocation == null)
                {
                    MessageBox.Show("Для списания выберите место хранения источника.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            case DocType.Outbound:
                if (fromLocation == null)
                {
                    MessageBox.Show("Для отгрузки выберите место хранения источника.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            case DocType.Move:
                if (fromLocation == null || toLocation == null)
                {
                    MessageBox.Show("Для перемещения выберите места хранения откуда/куда.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (fromLocation.Id == toLocation.Id)
                {
                    MessageBox.Show("Для перемещения места хранения откуда/куда должны быть разными.", "Операция", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            default:
                return true;
        }
    }
}
