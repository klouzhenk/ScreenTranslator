using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Capture;

public partial class SelectionWindow : Window
{
    private readonly Int32Rect _monitorBounds;

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
}
