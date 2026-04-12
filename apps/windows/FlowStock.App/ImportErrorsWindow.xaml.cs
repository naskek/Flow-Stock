using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class ImportErrorsWindow : Window
{
    private readonly AppServices _services;
    private readonly Action? _onDataChanged;
    private readonly ObservableCollection<ImportErrorView> _errors = new();
    private readonly ObservableCollection<Item> _items = new();
    private readonly ObservableCollection<Uom> _uoms = new();
    private ImportErrorView? _selectedError;

    public ImportErrorsWindow(AppServices services, Action? onDataChanged)
    {
        _services = services;
        _onDataChanged = onDataChanged;
        InitializeComponent();

        ErrorsGrid.ItemsSource = _errors;
        ItemsComboBox.ItemsSource = _items;
        NewItemUomCombo.ItemsSource = _uoms;

        LoadErrors();
        LoadItems();
        LoadUoms();
    }

    private void LoadErrors()
    {
        _errors.Clear();
        var errors = _services.WpfImportApi.TryGetImportErrors(null, out var apiErrors)
            ? apiErrors
            : _services.Import.GetImportErrors(null);
        foreach (var error in errors)
        {
            _errors.Add(error);
        }

        _selectedError = _errors.FirstOrDefault();
        ErrorsGrid.SelectedItem = _selectedError;
        UpdateSelection();
    }

    private void LoadItems()
    {
        _items.Clear();
        var items = _services.WpfReadApi.TryGetItems(null, out var apiItems)
            ? apiItems
            : _services.Catalog.GetItems(null);
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    private void LoadUoms()
    {
        _uoms.Clear();
        var uoms = _services.WpfCatalogApi.TryGetUoms(out var apiUoms)
            ? apiUoms
            : _services.Catalog.GetUoms();
        foreach (var uom in uoms)
        {
            _uoms.Add(uom);
        }
    }

    private void UpdateSelection()
    {
        SelectedBarcodeText.Text = _selectedError?.Barcode ?? string.Empty;
    }

    private void ErrorsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedError = ErrorsGrid.SelectedItem as ImportErrorView;
        UpdateSelection();
    }

    private async void BindBarcode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedError == null || string.IsNullOrWhiteSpace(_selectedError.Barcode))
        {
            MessageBox.Show("Выберите ошибку со штрихкодом.", "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ItemsComboBox.SelectedItem is not Item item)
        {
            MessageBox.Show("Выберите товар.", "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode != _selectedError.Barcode)
        {
            MessageBox.Show("У выбранного товара уже есть другой штрихкод.", "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var updatedItem = new Item
            {
                Id = item.Id,
                Name = item.Name,
                Barcode = _selectedError.Barcode,
                Gtin = item.Gtin,
                BaseUom = item.BaseUom,
                DefaultPackagingId = item.DefaultPackagingId,
                Brand = item.Brand,
                Volume = item.Volume,
                ShelfLifeMonths = item.ShelfLifeMonths,
                TaraId = item.TaraId,
                TaraName = item.TaraName,
                IsMarked = item.IsMarked,
                MaxQtyPerHu = item.MaxQtyPerHu
            };
            var result = await _services.WpfCatalogApi.TryUpdateItemAsync(updatedItem).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    throw new InvalidOperationException(result.Error);
                }

                _services.Catalog.AssignBarcode(item.Id, _selectedError.Barcode);
            }
            LoadItems();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CreateItem_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedError == null || string.IsNullOrWhiteSpace(_selectedError.Barcode))
        {
            MessageBox.Show("Выберите ошибку со штрихкодом.", "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var uom = (NewItemUomCombo.SelectedItem as Uom)?.Name;
            var candidate = new Item
            {
                Name = NewItemNameBox.Text?.Trim() ?? string.Empty,
                Barcode = _selectedError.Barcode,
                Gtin = string.IsNullOrWhiteSpace(NewItemGtinBox.Text) ? null : NewItemGtinBox.Text.Trim(),
                BaseUom = string.IsNullOrWhiteSpace(uom) ? "шт" : uom,
                IsMarked = false
            };
            var result = await _services.WpfCatalogApi.TryCreateItemAsync(candidate).ConfigureAwait(true);
            if (!result.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    throw new InvalidOperationException(result.Error);
                }

                _services.Catalog.CreateItem(candidate.Name, _selectedError.Barcode, candidate.Gtin, uom, null, null, null, null, false);
            }
            NewItemNameBox.Text = string.Empty;
            NewItemGtinBox.Text = string.Empty;
            NewItemUomCombo.SelectedItem = null;
            LoadItems();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Reapply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedError == null)
        {
            MessageBox.Show("Выберите ошибку.", "Ошибки импорта", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = await _services.WpfImportApi.TryReapplyErrorAsync(_selectedError.Id).ConfigureAwait(true);
        var applied = result.IsSuccess || _services.Import.ReapplyError(_selectedError.Id);
        if (!applied)
        {
            MessageBox.Show(
                result.Error ?? "Не удалось переприменить. Проверьте, что штрихкод привязан к товару.",
                "Ошибки импорта",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        LoadErrors();
        _onDataChanged?.Invoke();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadErrors();
        LoadItems();
        LoadUoms();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

