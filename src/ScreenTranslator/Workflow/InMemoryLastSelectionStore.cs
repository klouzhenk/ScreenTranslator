namespace ScreenTranslator.Workflow;

internal sealed class InMemoryLastSelectionStore : ILastSelectionStore
{
    public SelectionRegion? Last { get; set; }
}
