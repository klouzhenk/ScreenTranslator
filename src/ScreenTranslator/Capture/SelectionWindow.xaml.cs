using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Capture;

public partial class SelectionWindow : Window
{
    private readonly Int32Rect _monitorBounds;
    private Point? _dragStart;

    public SelectionWindow(BitmapSource frame, Int32Rect monitorBounds)
    {
        InitializeComponent();
        FrameImage.Source = frame;
        _monitorBounds = monitorBounds;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            _monitorBounds.X, _monitorBounds.Y,
            _monitorBounds.Width, _monitorBounds.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragStart = e.GetPosition(OverlayCanvas);
        CaptureMouse();
        SelectionRectangle.Visibility = Visibility.Visible;
        UpdateSelectionRectangle(_dragStart.Value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart is null) return;

        var current = e.GetPosition(OverlayCanvas);
        UpdateSelectionRectangle(current);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragStart is null) return;

        ReleaseMouseCapture();
        _dragStart = null;
    }

    private void UpdateSelectionRectangle(Point current)
    {
        if (_dragStart is null) return;

        var start = _dragStart.Value; 
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var w = Math.Abs(current.X - start.X);
        var h = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = w; 
        SelectionRectangle.Height = h;
    }
}
