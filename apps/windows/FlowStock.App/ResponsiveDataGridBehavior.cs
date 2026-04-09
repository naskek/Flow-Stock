using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfBinding = System.Windows.Data.Binding;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace FlowStock.App;

public static class ResponsiveDataGridBehavior
{
    private const double DefaultMinWidth = 70;
    private const double DefaultCompactMaxWidth = 180;
    private const double DefaultMediumMaxWidth = 340;
    private const double DefaultLongTextMaxWidth = 520;
    private const int MaxSampleRows = 48;
    private const double WidthPadding = 28;

    private static readonly Style DefaultTextColumnStyle = CreateDefaultTextColumnStyle();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ResponsiveDataGridBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(GridState),
            typeof(ResponsiveDataGridBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            var state = new GridState(grid);
            grid.SetValue(StateProperty, state);
            state.Attach();
            return;
        }

        if (grid.GetValue(StateProperty) is GridState existing)
        {
            existing.Detach();
            grid.ClearValue(StateProperty);
        }
    }

    private static Style CreateDefaultTextColumnStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Seal();
        return style;
    }

    private static void UpdateGridLayout(DataGrid grid)
    {
        if (!grid.IsLoaded || !grid.IsVisible || grid.ActualWidth < 160 || grid.Columns.Count == 0)
        {
            return;
        }

        ApplyDefaultTextStyles(grid);

        var columns = grid.Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .Select(column => BuildColumnLayout(grid, column))
            .Where(layout => layout != null)
            .Cast<ColumnLayout>()
            .ToList();

        if (columns.Count == 0)
        {
            return;
        }

        var availableWidth = Math.Max(
            0,
            grid.ActualWidth
            - 8
            - SystemParameters.VerticalScrollBarWidth
            - Math.Max(0, grid.RowHeaderWidth));

        if (availableWidth <= 0)
        {
            return;
        }

        foreach (var column in columns)
        {
            column.CurrentWidth = column.DesiredWidth;
        }

        var totalWidth = columns.Sum(column => column.CurrentWidth);
        if (totalWidth < availableWidth)
        {
            ExpandColumns(columns, availableWidth - totalWidth);
        }
        else if (totalWidth > availableWidth)
        {
            ShrinkColumns(columns, totalWidth - availableWidth);
        }

        foreach (var column in columns)
        {
            if (Math.Abs(column.Column.ActualWidth - column.CurrentWidth) < 1)
            {
                continue;
            }

            column.Column.Width = new DataGridLength(column.CurrentWidth, DataGridLengthUnitType.Pixel);
        }
    }

    private static void ApplyDefaultTextStyles(DataGrid grid)
    {
        foreach (var textColumn in grid.Columns.OfType<DataGridTextColumn>())
        {
            if (textColumn.ElementStyle == null)
            {
                textColumn.ElementStyle = DefaultTextColumnStyle;
            }
        }
    }

    private static ColumnLayout? BuildColumnLayout(DataGrid grid, DataGridColumn column)
    {
        var headerText = GetHeaderText(column);
        var explicitWidth = GetExplicitWidth(column.Width);
        var headerWidth = MeasureTextWidth(grid, headerText, FontWeights.SemiBold);
        var minWidth = Math.Max(
            column.MinWidth > 0 ? column.MinWidth : DefaultMinWidth,
            headerWidth + WidthPadding);

        if (column is DataGridCheckBoxColumn)
        {
            var desired = Math.Max(minWidth, explicitWidth > 0 ? explicitWidth : 84);
            var max = ResolveMaxWidth(column, desired, headerText, null, explicitWidth);
            return new ColumnLayout(column, desired, minWidth, max);
        }

        if (column is DataGridTemplateColumn)
        {
            var desired = Math.Max(minWidth, explicitWidth > 0 ? explicitWidth : headerWidth + 48);
            var max = ResolveMaxWidth(column, desired, headerText, null, explicitWidth);
            return new ColumnLayout(column, desired, minWidth, max);
        }

        if (column is not DataGridBoundColumn boundColumn)
        {
            var fallback = Math.Max(minWidth, explicitWidth > 0 ? explicitWidth : headerWidth + 32);
            var max = ResolveMaxWidth(column, fallback, headerText, null, explicitWidth);
            return new ColumnLayout(column, fallback, minWidth, max);
        }

        var binding = boundColumn.Binding as WpfBinding;
        var bindingPath = binding?.Path?.Path;
        var contentWidth = MeasureColumnContentWidth(grid, binding);
        var desiredWidth = Math.Max(minWidth, contentWidth);
        if (explicitWidth > 0)
        {
            desiredWidth = Math.Min(desiredWidth, explicitWidth);
            desiredWidth = Math.Max(desiredWidth, minWidth);
        }

        var maxWidth = ResolveMaxWidth(column, desiredWidth, headerText, bindingPath, explicitWidth);
        desiredWidth = Math.Min(desiredWidth, maxWidth);
        return new ColumnLayout(column, desiredWidth, minWidth, maxWidth);
    }

    private static double MeasureColumnContentWidth(DataGrid grid, WpfBinding? binding)
    {
        if (binding == null)
        {
            return DefaultMinWidth;
        }

        var headerWidth = MeasureTextWidth(grid, binding.Path?.Path ?? string.Empty, FontWeights.Normal);
        var maxWidth = headerWidth;
        var rowCount = 0;
        foreach (var item in EnumerateItems(grid.Items))
        {
            rowCount++;
            if (rowCount > MaxSampleRows)
            {
                break;
            }

            var value = GetBoundValue(item, binding);
            var display = FormatValue(value, binding);
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(grid, display, FontWeights.Normal));
        }

        return maxWidth + WidthPadding;
    }

    private static IEnumerable<object> EnumerateItems(IEnumerable items)
    {
        foreach (var item in items)
        {
            if (ReferenceEquals(item, CollectionView.NewItemPlaceholder))
            {
                continue;
            }

            if (item == null)
            {
                continue;
            }

            yield return item;
        }
    }

    private static object? GetBoundValue(object item, WpfBinding binding)
    {
        if (binding.Path == null || string.IsNullOrWhiteSpace(binding.Path.Path))
        {
            return item;
        }

        var current = item;
        foreach (var segment in binding.Path.Path.Split('.'))
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(
                segment,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        if (binding.Converter != null)
        {
            try
            {
                return binding.Converter.Convert(current, typeof(string), binding.ConverterParameter, binding.ConverterCulture);
            }
            catch
            {
                return current;
            }
        }

        return current;
    }

    private static string FormatValue(object? value, WpfBinding binding)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(binding.StringFormat))
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, binding.StringFormat, value);
            }
            catch
            {
                // fall through to default formatting
            }
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static string GetHeaderText(DataGridColumn column)
    {
        return column.Header switch
        {
            null => string.Empty,
            string text => text,
            TextBlock textBlock => textBlock.Text ?? string.Empty,
            _ => column.Header.ToString() ?? string.Empty
        };
    }

    private static double MeasureTextWidth(DataGrid grid, string text, FontWeight fontWeight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var dpi = VisualTreeHelper.GetDpi(grid);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(grid.FontFamily, grid.FontStyle, fontWeight, grid.FontStretch),
            grid.FontSize,
            WpfBrushes.Black,
            dpi.PixelsPerDip);

        return Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace);
    }

    private static double ResolveMaxWidth(
        DataGridColumn column,
        double desiredWidth,
        string headerText,
        string? bindingPath,
        double explicitWidth)
    {
        if (!double.IsNaN(column.MaxWidth) && !double.IsInfinity(column.MaxWidth) && column.MaxWidth > 0)
        {
            return column.MaxWidth;
        }

        if (explicitWidth > 0)
        {
            return Math.Max(explicitWidth, desiredWidth);
        }

        var descriptor = $"{headerText} {bindingPath}".ToLowerInvariant();
        var hardMax = IsCompactColumn(descriptor)
            ? DefaultCompactMaxWidth
            : IsLongTextColumn(descriptor)
                ? DefaultLongTextMaxWidth
                : DefaultMediumMaxWidth;

        var scaledMax = desiredWidth <= 120
            ? desiredWidth * 1.35
            : desiredWidth <= 220
                ? desiredWidth * 1.55
                : desiredWidth * 1.3;

        return Math.Max(desiredWidth, Math.Min(hardMax, scaledMax));
    }

    private static bool IsCompactColumn(string descriptor)
    {
        return ContainsAny(descriptor,
            " id",
            "id ",
            "код",
            "кол-во",
            "шт",
            "qty",
            "status",
            "статус",
            "дата",
            "created",
            "closed",
            "доступно",
            "остат",
            "hu",
            "gtin",
            "sku",
            "кодов",
            "ошибок");
    }

    private static bool IsLongTextColumn(string descriptor)
    {
        return ContainsAny(descriptor,
            "наименование",
            "товар",
            "комментар",
            "comment",
            "summary",
            "запрос",
            "partner",
            "контрагент",
            "reason",
            "причина",
            "location",
            "место",
            "файл",
            "file",
            "note",
            "name");
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(value.Contains);
    }

    private static double GetExplicitWidth(DataGridLength width)
    {
        return width.UnitType == DataGridLengthUnitType.Pixel
            ? width.Value
            : 0;
    }

    private static void ExpandColumns(IReadOnlyCollection<ColumnLayout> columns, double extraWidth)
    {
        var remaining = extraWidth;
        while (remaining > 0.5)
        {
            var expandable = columns
                .Where(column => column.CurrentWidth < column.MaxWidth - 0.5)
                .ToList();

            if (expandable.Count == 0)
            {
                return;
            }

            var weightSum = expandable.Sum(column => column.DesiredWidth);
            var used = 0d;
            foreach (var column in expandable)
            {
                var share = remaining * (column.DesiredWidth / weightSum);
                var growth = Math.Min(share, column.MaxWidth - column.CurrentWidth);
                if (growth <= 0)
                {
                    continue;
                }

                column.CurrentWidth += growth;
                used += growth;
            }

            if (used <= 0.5)
            {
                return;
            }

            remaining -= used;
        }
    }

    private static void ShrinkColumns(IReadOnlyCollection<ColumnLayout> columns, double overflowWidth)
    {
        var remaining = overflowWidth;
        while (remaining > 0.5)
        {
            var shrinkable = columns
                .Where(column => column.CurrentWidth > column.MinWidth + 0.5)
                .ToList();

            if (shrinkable.Count == 0)
            {
                return;
            }

            var weightSum = shrinkable.Sum(column => column.CurrentWidth);
            var used = 0d;
            foreach (var column in shrinkable)
            {
                var share = remaining * (column.CurrentWidth / weightSum);
                var shrink = Math.Min(share, column.CurrentWidth - column.MinWidth);
                if (shrink <= 0)
                {
                    continue;
                }

                column.CurrentWidth -= shrink;
                used += shrink;
            }

            if (used <= 0.5)
            {
                return;
            }

            remaining -= used;
        }
    }

    private sealed class ColumnLayout
    {
        public ColumnLayout(DataGridColumn column, double desiredWidth, double minWidth, double maxWidth)
        {
            Column = column;
            DesiredWidth = desiredWidth;
            MinWidth = minWidth;
            MaxWidth = Math.Max(maxWidth, minWidth);
            CurrentWidth = desiredWidth;
        }

        public DataGridColumn Column { get; }

        public double DesiredWidth { get; }

        public double MinWidth { get; }

        public double MaxWidth { get; }

        public double CurrentWidth { get; set; }
    }

    private sealed class GridState
    {
        private readonly DataGrid _grid;
        private readonly DependencyPropertyDescriptor _itemsSourceDescriptor;
        private bool _pendingUpdate;
        private bool _updating;

        public GridState(DataGrid grid)
        {
            _grid = grid;
            _itemsSourceDescriptor = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid));
        }

        public void Attach()
        {
            _grid.Loaded += OnGridChanged;
            _grid.SizeChanged += OnGridChanged;
            _grid.IsVisibleChanged += OnGridVisibilityChanged;
            _grid.AutoGeneratedColumns += OnGridChanged;
            _grid.Unloaded += OnUnloaded;
            _grid.TargetUpdated += OnGridChanged;
            _itemsSourceDescriptor.AddValueChanged(_grid, OnGridChanged);

            if (_grid.Columns is INotifyCollectionChanged columns)
            {
                columns.CollectionChanged += OnCollectionChanged;
            }

            if (_grid.Items is INotifyCollectionChanged items)
            {
                items.CollectionChanged += OnCollectionChanged;
            }

            ScheduleUpdate();
        }

        public void Detach()
        {
            _grid.Loaded -= OnGridChanged;
            _grid.SizeChanged -= OnGridChanged;
            _grid.IsVisibleChanged -= OnGridVisibilityChanged;
            _grid.AutoGeneratedColumns -= OnGridChanged;
            _grid.Unloaded -= OnUnloaded;
            _grid.TargetUpdated -= OnGridChanged;
            _itemsSourceDescriptor.RemoveValueChanged(_grid, OnGridChanged);

            if (_grid.Columns is INotifyCollectionChanged columns)
            {
                columns.CollectionChanged -= OnCollectionChanged;
            }

            if (_grid.Items is INotifyCollectionChanged items)
            {
                items.CollectionChanged -= OnCollectionChanged;
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _pendingUpdate = false;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScheduleUpdate();
        }

        private void OnGridChanged(object? sender, EventArgs e)
        {
            ScheduleUpdate();
        }

        private void OnGridVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            ScheduleUpdate();
        }

        private void ScheduleUpdate()
        {
            if (_pendingUpdate || _updating)
            {
                return;
            }

            _pendingUpdate = true;
            _grid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _pendingUpdate = false;
                if (_updating)
                {
                    return;
                }

                try
                {
                    _updating = true;
                    UpdateGridLayout(_grid);
                }
                finally
                {
                    _updating = false;
                }
            }));
        }
    }
}
