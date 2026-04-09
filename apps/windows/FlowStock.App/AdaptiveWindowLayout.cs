using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FlowStock.App;

public static class AdaptiveWindowLayout
{
    private const double WorkAreaMargin = 24;
    private const double FallbackChromeWidth = 24;
    private const double FallbackChromeHeight = 48;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AdaptiveWindowLayout),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(WindowState),
            typeof(AdaptiveWindowLayout),
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
        if (d is not Window window)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            var state = new WindowState(window);
            window.SetValue(StateProperty, state);
            state.Attach();
            return;
        }

        if (window.GetValue(StateProperty) is WindowState existing)
        {
            existing.Detach();
            window.ClearValue(StateProperty);
        }
    }

    private sealed class WindowState
    {
        private readonly Window _window;
        private bool _pending;
        private bool _wrappedInScrollViewer;
        private FrameworkElement? _rootContent;

        public WindowState(Window window)
        {
            _window = window;
        }

        public void Attach()
        {
            _window.Loaded += OnWindowChanged;
            _window.ContentRendered += OnWindowChanged;
            _window.SizeChanged += OnWindowChanged;
            _window.LocationChanged += OnWindowChanged;
            ScheduleApply();
        }

        public void Detach()
        {
            _window.Loaded -= OnWindowChanged;
            _window.ContentRendered -= OnWindowChanged;
            _window.SizeChanged -= OnWindowChanged;
            _window.LocationChanged -= OnWindowChanged;
        }

        private void OnWindowChanged(object? sender, EventArgs e)
        {
            ScheduleApply();
        }

        private void ScheduleApply()
        {
            if (_pending)
            {
                return;
            }

            _pending = true;
            _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _pending = false;
                Apply();
            }));
        }

        private void Apply()
        {
            if (!_window.IsLoaded)
            {
                return;
            }

            var workArea = SystemParameters.WorkArea;
            var maxWidth = Math.Max(320, workArea.Width - WorkAreaMargin);
            var maxHeight = Math.Max(240, workArea.Height - WorkAreaMargin);

            if (double.IsNaN(_window.MaxWidth) || double.IsInfinity(_window.MaxWidth) || _window.MaxWidth <= 0)
            {
                _window.MaxWidth = maxWidth;
            }
            else
            {
                _window.MaxWidth = Math.Min(_window.MaxWidth, maxWidth);
            }

            if (double.IsNaN(_window.MaxHeight) || double.IsInfinity(_window.MaxHeight) || _window.MaxHeight <= 0)
            {
                _window.MaxHeight = maxHeight;
            }
            else
            {
                _window.MaxHeight = Math.Min(_window.MaxHeight, maxHeight);
            }

            if (_window is MainWindow)
            {
                ClampToWorkArea(workArea);
                return;
            }

            _rootContent ??= ResolveRootContent();
            if (_rootContent == null)
            {
                ClampToWorkArea(workArea);
                return;
            }

            var chromeWidth = _window.ActualWidth > 0 && _rootContent.ActualWidth > 0
                ? Math.Max(FallbackChromeWidth, _window.ActualWidth - _rootContent.ActualWidth)
                : FallbackChromeWidth;
            var chromeHeight = _window.ActualHeight > 0 && _rootContent.ActualHeight > 0
                ? Math.Max(FallbackChromeHeight, _window.ActualHeight - _rootContent.ActualHeight)
                : FallbackChromeHeight;
            var currentWidth = !double.IsNaN(_window.Width) && _window.Width > 0
                ? _window.Width
                : Math.Max(_window.ActualWidth, _rootContent.ActualWidth + chromeWidth);
            var currentHeight = !double.IsNaN(_window.Height) && _window.Height > 0
                ? _window.Height
                : Math.Max(_window.ActualHeight, _rootContent.ActualHeight + chromeHeight);

            var clientMaxWidth = Math.Max(240, _window.MaxWidth - chromeWidth);
            var clientMaxHeight = Math.Max(160, _window.MaxHeight - chromeHeight);

            _rootContent.Measure(new System.Windows.Size(clientMaxWidth, double.PositiveInfinity));
            var desiredWidth = Math.Min(_window.MaxWidth, Math.Max(currentWidth, _rootContent.DesiredSize.Width + chromeWidth));
            var desiredHeight = Math.Min(_window.MaxHeight, Math.Max(currentHeight, _rootContent.DesiredSize.Height + chromeHeight));

            if (desiredWidth > currentWidth + 1)
            {
                _window.Width = desiredWidth;
            }

            if (desiredHeight > currentHeight + 1)
            {
                _window.Height = desiredHeight;
            }

            ClampToWorkArea(workArea);

            var actualClientHeight = Math.Max(0, _window.ActualHeight - chromeHeight);
            var actualClientWidth = Math.Max(0, _window.ActualWidth - chromeWidth);
            if (_wrappedInScrollViewer
                || _rootContent is ScrollViewer
                || _rootContent.DesiredSize.Height <= actualClientHeight + 2 && _rootContent.DesiredSize.Width <= actualClientWidth + 2)
            {
                return;
            }

            WrapContentInScrollViewer();
        }

        private FrameworkElement? ResolveRootContent()
        {
            return _window.Content as FrameworkElement;
        }

        private void WrapContentInScrollViewer()
        {
            if (_rootContent == null || _wrappedInScrollViewer || _window.Content is ScrollViewer)
            {
                return;
            }

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = false,
                Content = _rootContent
            };

            _window.Content = scrollViewer;
            _wrappedInScrollViewer = true;
        }

        private void ClampToWorkArea(Rect workArea)
        {
            if (_window.Width > workArea.Width)
            {
                _window.Width = workArea.Width;
            }

            if (_window.Height > workArea.Height)
            {
                _window.Height = workArea.Height;
            }

            if (_window.Left < workArea.Left)
            {
                _window.Left = workArea.Left;
            }

            if (_window.Top < workArea.Top)
            {
                _window.Top = workArea.Top;
            }

            var right = _window.Left + _window.Width;
            if (right > workArea.Right)
            {
                _window.Left = Math.Max(workArea.Left, workArea.Right - _window.Width);
            }

            var bottom = _window.Top + _window.Height;
            if (bottom > workArea.Bottom)
            {
                _window.Top = Math.Max(workArea.Top, workArea.Bottom - _window.Height);
            }
        }
    }
}
