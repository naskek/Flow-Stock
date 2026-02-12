using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FlowStock.Core.Models;

namespace FlowStock.App;

public partial class KmBatchDetailsWindow : Window
{
    private readonly AppServices _services;
    private readonly KmCodeBatch _batch;
    private readonly ObservableCollection<KmCodeRow> _codes = new();
    private readonly List<StatusOption> _statusOptions = new()
    {
        new StatusOption(null, "Все"),
        new StatusOption(KmCodeStatus.InPool, "В пуле"),
        new StatusOption(KmCodeStatus.OnHand, "На складе"),
        new StatusOption(KmCodeStatus.Shipped, "Отгружен")
    };

    public KmBatchDetailsWindow(AppServices services, KmCodeBatch batch)
    {
        _services = services;
        _batch = batch;
        InitializeComponent();

        CodesGrid.ItemsSource = _codes;
        StatusFilter.ItemsSource = _statusOptions;
        StatusFilter.SelectedIndex = 0;
        LoadCodes();
    }

    private void LoadCodes()
    {
        _codes.Clear();
        var search = SearchBox.Text?.Trim();
        var status = (StatusFilter.SelectedItem as StatusOption)?.Status;
        foreach (var code in _services.Km.GetCodes(_batch.Id, search, status))
        {
            _codes.Add(new KmCodeRow
            {
                StatusDisplay = KmCodeStatusMapper.ToDisplayName(code.Status),
                Gtin14 = code.Gtin14,
                SkuDisplay = code.SkuBarcode ?? code.Gtin14 ?? string.Empty,
                NameDisplay = ResolveName(code),
                CodeDisplay = BuildCodeDisplay(code.CodeRaw),
                HuCode = code.HuCode ?? string.Empty,
                LocationCode = code.LocationCode ?? string.Empty
            });
        }

        var unmatched = _services.Km.CountUnmatchedSku(_batch.Id);
        UnmatchedText.Text = unmatched > 0 ? $"Не сопоставлено SKU: {unmatched}" : string.Empty;
    }

    private static string ResolveName(KmCode code)
    {
        if (!string.IsNullOrWhiteSpace(code.SkuName))
        {
            return code.SkuName;
        }

        if (!string.IsNullOrWhiteSpace(code.ProductName))
        {
            return code.ProductName;
        }

        return string.Empty;
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

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        LoadCodes();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        StatusFilter.SelectedIndex = 0;
        LoadCodes();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadCodes();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            LoadCodes();
        }
    }

    private sealed record StatusOption(KmCodeStatus? Status, string Name);

    private sealed class KmCodeRow
    {
        public string StatusDisplay { get; init; } = string.Empty;
        public string? Gtin14 { get; init; }
        public string SkuDisplay { get; init; } = string.Empty;
        public string NameDisplay { get; init; } = string.Empty;
        public string CodeDisplay { get; init; } = string.Empty;
        public string HuCode { get; init; } = string.Empty;
        public string LocationCode { get; init; } = string.Empty;
    }
}
