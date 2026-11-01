using ScreenTranslator.Capture;
using ScreenTranslator.Configuration;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScreenTranslator.Translation;

public partial class ResultWindow : Window
{
    private readonly DispatcherTimer _autoHideTimer;
    private readonly Int32Rect? _selectionScreenRect;
    private readonly Int32Rect _monitorBounds;

    public static void ShowTranslation(string original, string translated, ResultWindowOptions options, Int32Rect selectionScreenRect, Int32Rect monitorBounds)
    {
        new ResultWindow(original, translated, isError: false, options, selectionScreenRect, monitorBounds).Show();
    }

    public static void ShowError(string? original, string message, ResultWindowOptions options, Int32Rect selectionScreenRect, Int32Rect monitorBounds)
    {
        new ResultWindow(original, message, isError: true, options, selectionScreenRect, monitorBounds).Show();
    }

    public static void ShowCentered(string message, ResultWindowOptions options, Int32Rect monitorBounds)
    {
        new ResultWindow(original: null, message, isError: true, options, selectionScreenRect: null, monitorBounds).Show();
    }

    private ResultWindow(
        string? original,
        string translated,
        bool isError,
        ResultWindowOptions options,
        Int32Rect? selectionScreenRect,
        Int32Rect monitorBounds)
    {
        InitializeComponent();

        if (original is null)
            OriginalText.Visibility = Visibility.Collapsed;
        else
            OriginalText.Text = original;

        TranslatedText.Text = translated;
        OriginalText.FontSize = options.FontSize;
        TranslatedText.FontSize = options.FontSize;

        if (isError)
            BorderBrush = Brushes.IndianRed;

        _selectionScreenRect = selectionScreenRect;
        _monitorBounds = monitorBounds;

        _autoHideTimer = new() { Interval = TimeSpan.FromSeconds(options.AutoHideSeconds) };
        _autoHideTimer.Tick += (_, _) => Close();

        ContentRendered += (_, _) => PositionWindow();
    }
    
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        var newStyle = exStyle | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, newStyle);
        
        _autoHideTimer.Start();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _autoHideTimer.Stop();
    }
    
    private void PositionWindow()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        var heightPx = (int)Math.Round(ActualHeight * dpi.DpiScaleY);

        var (targetX, targetY) = _selectionScreenRect is { } rect
            ? GetPositionNextToSelection(rect, widthPx, heightPx)
            : GetCenteredPosition(widthPx, heightPx);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            targetX, targetY, 0, 0,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private (int X, int Y) GetPositionNextToSelection(Int32Rect rect, int widthPx, int heightPx)
    {
        const int padding = 10;

        var targetX = rect.X;
        var targetY = rect.Y + rect.Height + padding;

        if (targetY + heightPx > _monitorBounds.Y + _monitorBounds.Height)
            targetY = rect.Y - heightPx - padding;

        var monitorRight = _monitorBounds.X + _monitorBounds.Width;
        if (targetX + widthPx > monitorRight)
            targetX = monitorRight - widthPx;
        if (targetX < _monitorBounds.X)
            targetX = _monitorBounds.X;

        var monitorBottom = _monitorBounds.Y + _monitorBounds.Height;
        if (targetY + heightPx > monitorBottom)
            targetY = monitorBottom - heightPx;
        if (targetY < _monitorBounds.Y)
            targetY = _monitorBounds.Y;

        return (targetX, targetY);
    }

    private (int X, int Y) GetCenteredPosition(int widthPx, int heightPx)
    {
        return (
            _monitorBounds.X + (_monitorBounds.Width - widthPx) / 2,
            _monitorBounds.Y + (_monitorBounds.Height - heightPx) / 2
        );
    }
}
