namespace ScreenTranslator.Workflow;

internal interface IScreenTranslationWorkflow
{
    Task SelectAndTranslateAsync();
    Task RepeatLastAsync();
}
