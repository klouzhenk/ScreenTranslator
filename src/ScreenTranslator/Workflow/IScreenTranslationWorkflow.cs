namespace ScreenTranslator.Workflow;

public interface IScreenTranslationWorkflow
{
    Task SelectAndTranslateAsync();
    Task RepeatLastAsync();
}
