using System;
using System.Windows;

namespace FlowStock.App;

public partial class AdminWindow : Window
{
    private readonly AppServices _services;
    private readonly Action<bool>? _onDeleteModeChanged;
    private readonly Action? _onOperationsCleared;
    private bool _deleteModeEnabled;

    public AdminWindow(AppServices services, bool deleteModeEnabled, Action<bool>? onDeleteModeChanged, Action? onOperationsCleared = null)
    {
        _services = services;
        _onDeleteModeChanged = onDeleteModeChanged;
        _onOperationsCleared = onOperationsCleared;
        _deleteModeEnabled = deleteModeEnabled;

        InitializeComponent();
        DatabasePathBox.Text = _services.DatabasePath;
        UpdateDeleteModeUi();
    }

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _services.Backups.CreateBackup("admin_manual");
            var settings = _services.Settings.Load();
            _services.Backups.ApplyRetention(settings.KeepLastNBackups);
            _services.AdminLogger.Info($"admin_backup path={path}");
            MessageBox.Show("Резервная копия создана.", "Администрирование", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _services.AdminLogger.Error("admin_backup failed", ex);
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleDeleteMode_Click(object sender, RoutedEventArgs e)
    {
        var newState = !_deleteModeEnabled;
        var actionText = newState ? "включить" : "выключить";
        var confirm = MessageBox.Show(
            $"Подтвердите: {actionText} режим удаления записей во вкладках.",
            "Администрирование",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _deleteModeEnabled = newState;
        _onDeleteModeChanged?.Invoke(_deleteModeEnabled);
        _services.AdminLogger.Info($"admin_delete_mode enabled={_deleteModeEnabled}");
        UpdateDeleteModeUi();
    }

    private void ClearOperations_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Очистить все операции и заказы? Это действие удалит тестовые движения.",
            "Администрирование",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var backupDecision = MessageBox.Show(
            "Сделать резервную копию перед очисткой?",
            "Администрирование",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);
        if (backupDecision == MessageBoxResult.Cancel)
        {
            return;
        }

        if (backupDecision == MessageBoxResult.Yes && !TryCreateBackup("admin_before_reset_movements"))
        {
            return;
        }

        try
        {
            _services.Admin.ResetMovements();
            _services.AdminLogger.Info("admin_reset_movements from ui");
            _onOperationsCleared?.Invoke();
            MessageBox.Show("Операции очищены.", "Администрирование", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _services.AdminLogger.Error("admin_reset_movements failed", ex);
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDeleteModeUi()
    {
        if (_deleteModeEnabled)
        {
            DeleteModeStatusText.Text = "Статус: включен (доступно удаление строк во вкладках)";
            ToggleDeleteModeButton.Content = "Выключить режим удаления";
            return;
        }

        DeleteModeStatusText.Text = "Статус: выключен (удаление строк заблокировано)";
        ToggleDeleteModeButton.Content = "Включить режим удаления";
    }

    private bool TryCreateBackup(string reason)
    {
        try
        {
            var path = _services.Backups.CreateBackup(reason);
            var settings = _services.Settings.Load();
            _services.Backups.ApplyRetention(settings.KeepLastNBackups);
            _services.AdminLogger.Info($"admin_backup reason={reason} path={path}");
            return true;
        }
        catch (Exception ex)
        {
            _services.AdminLogger.Error("admin_backup failed", ex);
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
