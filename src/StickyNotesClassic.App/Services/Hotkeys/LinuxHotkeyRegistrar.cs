using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StickyNotesClassic.App.Services.Hotkeys;

/// <summary>
/// Registers global hotkeys on Linux via X11.
/// </summary>
public sealed class LinuxHotkeyRegistrar : IHotkeyRegistrar
{
    private readonly ILogger<LinuxHotkeyRegistrar> _logger;
    private readonly string? _displayName;

    private IntPtr _display;
    private uint _modifierMask;
    private int _keyCode;
    private Thread? _eventThread;
    private CancellationTokenSource? _eventScope;

    public LinuxHotkeyRegistrar(ILogger<LinuxHotkeyRegistrar> logger)
    {
        _logger = logger;
        _displayName = Environment.GetEnvironmentVariable("DISPLAY");
    }

    public bool IsSupported => OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(_displayName);

    public string? UnsupportedReason => IsSupported ? null : "Requires X11 and DISPLAY to be available.";

    public Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct)
    {
        if (!IsSupported)
        {
            _logger.LogInformation("X11 hotkeys unsupported. DISPLAY missing or OS not Linux.");
            return Task.FromResult(false);
        }

        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
        {
            _logger.LogWarning("Unable to open X11 display '{Display}' for global hotkeys.", _displayName);
            return Task.FromResult(false);
        }

        _keyCode = (int)XKeysymToKeycode(_display, XStringToKeysym(key));
        if (_keyCode == 0)
        {
            _logger.LogWarning("Unknown X11 keysym for hotkey key '{Key}'.", key);
            CleanupDisplay();
            return Task.FromResult(false);
        }

        _modifierMask = TranslateModifiers(modifiers);
        var root = XDefaultRootWindow(_display);
        XGrabKey(_display, _keyCode, _modifierMask, root, owner_events: true, GrabModeAsync, GrabModeAsync);
        XSelectInput(_display, root, KeyPressMask);
        XSync(_display, discard: false);

        _eventScope?.Cancel();
        _eventScope?.Dispose();
        _eventScope = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _eventThread = new Thread(() => EventLoop(onTriggered, _eventScope.Token))
        {
            IsBackground = true,
            Name = "X11HotkeyListener"
        };
        _eventThread.Start();

        _logger.LogInformation("Registered X11 hotkey {Modifiers}+{Key}.", modifiers, key);
        return Task.FromResult(true);
    }

    public Task UnregisterAsync(CancellationToken ct)
    {
        _eventScope?.Cancel();
        if (_eventThread is { IsAlive: true })
        {
            _eventThread.Join(TimeSpan.FromSeconds(1));
        }

        if (_display != IntPtr.Zero && _keyCode != 0)
        {
            var root = XDefaultRootWindow(_display);
            XUngrabKey(_display, _keyCode, _modifierMask, root);
            XFlush(_display);
        }

        CleanupDisplay();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _eventScope?.Cancel();
        CleanupDisplay();
    }

    private void EventLoop(Action onTriggered, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _display != IntPtr.Zero)
        {
            var result = XNextEvent(_display, out var xEvent);
            if (result != 0)
            {
                _logger.LogWarning("XNextEvent returned error code {Result}.", result);
                continue;
            }

            if (xEvent.type == KeyPress && xEvent.KeyEvent.keycode == _keyCode &&
                (_modifierMask == 0 || (xEvent.KeyEvent.state & _modifierMask) == _modifierMask))
            {
                onTriggered();
            }
        }
    }

    private static uint TranslateModifiers(string modifiers)
    {
        var mask = 0u;
        var tokens = modifiers.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim());

        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "shift":
                    mask |= ShiftMask;
                    break;
                case "control":
                case "ctrl":
                    mask |= ControlMask;
                    break;
                case "alt":
                    mask |= Mod1Mask;
                    break;
                case "super":
                case "win":
                    mask |= Mod4Mask;
                    break;
            }
        }

        return mask;
    }

    private void CleanupDisplay()
    {
        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    private const int GrabModeAsync = 1;
    private const long KeyPressMask = 1 << 0;
    private const int KeyPress = 2;
    private const uint ShiftMask = 1;
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3; // Alt
    private const uint Mod4Mask = 1 << 6; // Super/Win

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XKeysymToKeycode(IntPtr display, IntPtr keysym);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XStringToKeysym(string str);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window, bool owner_events, int pointer_mode, int keyboard_mode);

    [DllImport("libX11.so.6")]
    private static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window);

    [DllImport("libX11.so.6")]
    private static extern int XSelectInput(IntPtr display, IntPtr window, long event_mask);

    [DllImport("libX11.so.6")]
    private static extern int XNextEvent(IntPtr display, out XEvent xevent);

    [DllImport("libX11.so.6")]
    private static extern int XSync(IntPtr display, bool discard);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [StructLayout(LayoutKind.Sequential)]
    private struct XKeyEvent
    {
        public int type;
        public IntPtr serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public IntPtr time;
        public int x;
        public int y;
        public int x_root;
        public int y_root;
        public uint state;
        public int keycode;
        public bool same_screen;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct XEvent
    {
        [FieldOffset(0)]
        public int type;

        [FieldOffset(0)]
        public XKeyEvent KeyEvent;
    }
}
