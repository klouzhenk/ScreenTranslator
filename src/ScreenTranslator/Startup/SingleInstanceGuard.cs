namespace ScreenTranslator.Startup;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsOnlyInstance { get; }

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
        IsOnlyInstance = createdNew;
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
