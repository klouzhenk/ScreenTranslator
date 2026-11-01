namespace ScreenTranslator.Workflow;

public interface ILastSelectionStore
{
    SelectionRegion? Last { get; set; }
}
