using System.Windows;

namespace ScreenTranslator.Workflow;

public readonly record struct SelectionRegion(Int32Rect ScreenRect, Int32Rect MonitorBounds);
