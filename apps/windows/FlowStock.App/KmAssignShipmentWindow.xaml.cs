using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class KmAssignShipmentWindow : Window
{
    private readonly AppServices _services;
    private readonly Doc _doc;
    private readonly DocLine _line;
    private readonly Item _item;
    private readonly ObservableCollection<KmCodeRow> _codes = new();

    public KmAssignShipmentWindow(AppServices services, Doc doc, DocLine line, Item item)
    {
        _services = services;
        _doc = doc;
        _line = line;
        _item = item;
        InitializeComponent();

        CodesGrid.ItemsSource = _codes;
        LoadCodes();
    }

    private void LoadCodes()
    {
        _codes.Clear();
        foreach (var code in _services.Km.GetShipmentCodes(_line.Id))
        {
            _codes.Add(new KmCodeRow
            {
                CodeDisplay = BuildCodeDisplay(code.CodeRaw),
                StatusDisplay = KmCodeStatusMapper.ToDisplayName(code.Status)
            });
        }

        ItemText.Text = _item.Name;
        CountText.Text = $"Нужно: {FormatQty(_line.Qty)} | Привязано: {_services.Km.GetAssignedCountForShipmentLine(_line.Id)}";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        TryAddCode();
    }

    private void CodeInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TryAddCode();
        }
    }

    private void TryAddCode()
    {
        var codeRaw = CodeInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(codeRaw))
        {
            return;
        }

        var rounded = Math.Round(_line.Qty);
        if (Math.Abs(_line.Qty - rounded) > 0.0001)
        {
            MessageBox.Show("Количество для маркируемого товара должно быть целым.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var required = (int)rounded;
        var assigned = _services.Km.GetAssignedCountForShipmentLine(_line.Id);
        if (assigned >= required)
        {
            MessageBox.Show("Достигнуто требуемое количество кодов.", "Маркировка", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _services.Km.AssignCodeToShipment(codeRaw, _doc.Id, _line, _item, _doc.OrderId);
            CodeInput.Text = string.Empty;
            LoadCodes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Маркировка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string BuildCodeDisplay(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        const int max = 48;
        if (raw.Length <= max)
        {
            return raw;
        }

        return raw.Substring(0, max) + "...";
    }

    private static string FormatQty(double value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private sealed class KmCodeRow
    {
        public string CodeDisplay { get; init; } = string.Empty;
        public string StatusDisplay { get; init; } = string.Empty;
    }
}
