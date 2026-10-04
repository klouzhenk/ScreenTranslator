using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenTranslator.HotKeys;

public sealed class GlobalHotkeyService : IHotkeyService, IDisposable
{
    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly HwndSource _source;
    private readonly ConcurrentDictionary<int, Action> _handlers = new();
    private int _nextId;
    private bool _disposed;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService> logger)
    {
        _logger = logger;

        var parameters = new HwndSourceParameters("ScreenTranslator.HotkeyWindow")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            Width = 0,
            Height = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IDisposable Register(string hotKey, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var (modifiers, vk) = HotkeyParser.Parse(hotKey);
        modifiers |= NativeMethods.MOD_NOREPEAT;

        var id = Interlocked.Increment(ref _nextId);

        if (!NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, vk))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"RegisterHotKey failed for '{hotKey}'. Win32 error: {error}.");
        }

        _handlers[id] = callback;
        _logger.LogInformation("Hotkey registered: {Hotkey} (id={Id})", hotKey, id);
        return new Registration(this, id, hotKey);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _handlers.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);

        _handlers.Clear();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private sealed class Registration(GlobalHotkeyService service, int id, string hotKey) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                service.Unregister(id, hotKey);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is not NativeMethods.WM_HOTKEY) return IntPtr.Zero;

        var id = wParam.ToInt32();
        if (_handlers.TryGetValue(id, out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotkey handler threw (id={Id})", id);
            }

            handled = true;
        }

        return IntPtr.Zero;
    }

    private void Unregister(int id, string hotKey)
    {
        if (_disposed) return;

        if (NativeMethods.UnregisterHotKey(_source.Handle, id))
        {
            _handlers.TryRemove(id, out _);
            _logger.LogInformation("Hotkey unregistered: {Hotkey} (id = {Id})", hotKey, id);
        }
        else
        {
            _logger.LogWarning("UnregisterHotKey failed for '{Hotkey}' (id = {Id})", hotKey, id);
        }
    }
}
