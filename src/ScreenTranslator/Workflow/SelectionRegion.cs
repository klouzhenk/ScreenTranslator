using System.Windows;

namespace ScreenTranslator.Workflow;

internal readonly record struct SelectionRegion(Int32Rect ScreenRect, Int32Rect MonitorBounds);
