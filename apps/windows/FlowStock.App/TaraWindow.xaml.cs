using System.Collections.ObjectModel;
using System.Windows;
using FlowStock.Core.Models;
using Npgsql;

namespace FlowStock.App;

public partial class TaraWindow : Window
{
    private readonly AppServices _services;
    private readonly ObservableCollection<Tara> _taras = new();
    private readonly Action? _onChanged;
    private Tara? _selectedTara;

    public TaraWindow(AppServices services, Action? onChanged)
    {
        _services = services;
        _onChanged = onChanged;
        InitializeComponent();

        TarasGrid.ItemsSource = _taras;
        LoadTaras();
        UpdateDeleteButton();
    }

    private void LoadTaras()
    {
        _taras.Clear();
        foreach (var tara in _services.Catalog.GetTaras())
        {
            _taras.Add(tara);
        }

        UpdateDeleteButton();
    }

    private void AddTara_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TaraNameBox.Text))
        {
            MessageBox.Show("Введите наименование тары.", "Тара", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _services.Catalog.CreateTara(TaraNameBox.Text);
            TaraNameBox.Text = string.Empty;
            LoadTaras();
            _onChanged?.Invoke();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Тара", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (PostgresException)
        {
            MessageBox.Show("Такая тара уже существует.", "Тара", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Тара", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteTara_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTara == null)
        {
            MessageBox.Show("Выберите тару.", "Тара", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Удалить тару \"{_selectedTara.Name}\"?",
            "Тара",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _services.Catalog.DeleteTara(_selectedTara.Id);
            LoadTaras();
            _onChanged?.Invoke();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Тара", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Тара", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Тара", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TarasGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedTara = TarasGrid.SelectedItem as Tara;
        UpdateDeleteButton();
    }

    private void UpdateDeleteButton()
    {
        if (DeleteTaraButton != null)
        {
            DeleteTaraButton.IsEnabled = _selectedTara != null;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
