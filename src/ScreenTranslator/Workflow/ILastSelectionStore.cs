namespace ScreenTranslator.Workflow;

internal interface ILastSelectionStore
{
    SelectionRegion? Last { get; set; }
}
