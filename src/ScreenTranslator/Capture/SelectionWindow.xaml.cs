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
    private Rect _selection = new(0, 0, 0, 0);
    private readonly TaskCompletionSource<Int32Rect?> _resultTcs = new();

    public Task<Int32Rect?> Result => _resultTcs.Task;

    public SelectionWindow(BitmapSource frame, Int32Rect monitorBounds)
    {
        InitializeComponent();
        FrameImage.Source = frame;
        _monitorBounds = monitorBounds;
        Loaded += (_, _) => UpdateOverlay();
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
        if (e.Key is not Key.Escape) return;

        _resultTcs.TrySetResult(null);
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragStart = e.GetPosition(OverlayCanvas);
        CaptureMouse();
        UpdateSelection(_dragStart.Value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragStart is null) return;

        var current = e.GetPosition(OverlayCanvas);
        UpdateSelection(current);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragStart is null) return;

        ReleaseMouseCapture();
        _dragStart = null;

        // TODO : use some constants
        if (_selection.Width < 5 || _selection.Height < 5) return;

        _resultTcs.TrySetResult(DipToPhysical(_selection));
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _resultTcs.TrySetResult(null);
    }

    private Int32Rect? DipToPhysical(Rect selection)
    {
        var source = (BitmapSource)FrameImage.Source;
        var scaleX = source.PixelWidth / OverlayCanvas.ActualWidth; 
        var scaleY = source.PixelHeight / OverlayCanvas.ActualHeight;

        return new Int32Rect(
            (int)Math.Round(selection.X * scaleX),
            (int)Math.Round(selection.Y * scaleY),
            (int)Math.Round(selection.Width * scaleX),
            (int)Math.Round(selection.Height * scaleY));
    }

    private void UpdateSelection(Point current)
    {
        if (_dragStart is null) return;

        var start = _dragStart.Value;
        _selection = new Rect(
            Math.Min(start.X, current.X),
            Math.Min(start.Y, current.Y),
            Math.Abs(current.X - start.X),
            Math.Abs(current.Y - start.Y));

        UpdateOverlay();
    }

    private void UpdateOverlay()
    {
        FullArea.Rect = new Rect(0, 0, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);
        SelectionArea.Rect = _selection;

        Canvas.SetLeft(SelectionRectangle, _selection.X);
        Canvas.SetTop(SelectionRectangle, _selection.Y);
        SelectionRectangle.Width = _selection.Width;
        SelectionRectangle.Height = _selection.Height;
    }
}
