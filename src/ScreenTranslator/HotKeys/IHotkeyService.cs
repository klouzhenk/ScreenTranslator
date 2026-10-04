namespace ScreenTranslator.HotKeys;

public interface IHotkeyService
{
    IDisposable Register(string hotKey, Action callback);
}
