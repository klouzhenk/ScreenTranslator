using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenTranslator.Capture;

public partial class PreviewWindow : Window
{
    public PreviewWindow(BitmapSource image)
    {
        InitializeComponent();
        PreviewImage.Source = image;
    }
}
