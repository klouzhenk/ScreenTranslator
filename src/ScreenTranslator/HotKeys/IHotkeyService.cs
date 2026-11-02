namespace ScreenTranslator.HotKeys;

internal interface IHotkeyService
{
    IDisposable Register(string hotKey, Action callback);
}
