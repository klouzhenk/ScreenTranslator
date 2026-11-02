using System.Windows;

namespace ScreenTranslator.Translation;

internal static class ResultWindowPositioner
{
    private const int Padding = 10;

    public static (int X, int Y) GetPositionNextToSelection(Int32Rect rect, Int32Rect monitorBounds, int widthPx, int heightPx)
    {
        var targetX = rect.X;
        var targetY = rect.Y + rect.Height + Padding;

        if (targetY + heightPx > monitorBounds.Y + monitorBounds.Height)
            targetY = rect.Y - heightPx - Padding;

        var monitorRight = monitorBounds.X + monitorBounds.Width;
        if (targetX + widthPx > monitorRight)
            targetX = monitorRight - widthPx;
        if (targetX < monitorBounds.X)
            targetX = monitorBounds.X;

        var monitorBottom = monitorBounds.Y + monitorBounds.Height;
        if (targetY + heightPx > monitorBottom)
            targetY = monitorBottom - heightPx;
        if (targetY < monitorBounds.Y)
            targetY = monitorBounds.Y;

        return (targetX, targetY);
    }

    public static (int X, int Y) GetCenteredPosition(Int32Rect monitorBounds, int widthPx, int heightPx)
    {
        return (
            monitorBounds.X + (monitorBounds.Width - widthPx) / 2,
            monitorBounds.Y + (monitorBounds.Height - heightPx) / 2
        );
    }
}
