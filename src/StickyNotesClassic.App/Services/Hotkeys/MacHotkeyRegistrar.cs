using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StickyNotesClassic.App.Services.Hotkeys;

[SupportedOSPlatform("macos")]
public sealed class MacHotkeyRegistrar : IHotkeyRegistrar
{
    private readonly ILogger<MacHotkeyRegistrar> _logger;
    private CGEventTapCallback? _eventCallback;
    private IntPtr _eventTap;
    private IntPtr _runLoop;
    private Task? _runLoopTask;
    private Action? _onTriggered;
    private string _targetKey = string.Empty;
    private CGEventFlags _targetModifiers;

    public MacHotkeyRegistrar(ILogger<MacHotkeyRegistrar> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsMacOS();

    public string? UnsupportedReason => IsSupported
        ? null
        : "Only available on macOS.";

    public Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct)
    {
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!NativeMethods.CGPreflightListenEventAccess())
        {
            _logger.LogWarning("Accessibility permission required for global hotkeys");
            return Task.FromResult(false);
        }

        _targetModifiers = ParseModifiers(modifiers);
        _targetKey = key?.Trim() ?? string.Empty;
        _onTriggered = onTriggered;

        UnregisterInternal();

        _eventCallback = EventTapCallback;
        var mask = NativeMethods.CGEventMaskBit(NativeMethods.CGEventType.KeyDown);
        _eventTap = NativeMethods.CGEventTapCreate(
            NativeMethods.CGEventTapLocation.Session,
            NativeMethods.CGEventTapPlacement.HeadInsert,
            NativeMethods.CGEventTapOptions.ListenOnly,
            mask,
            _eventCallback,
            IntPtr.Zero);

        if (_eventTap == IntPtr.Zero)
        {
            _logger.LogWarning("Failed to create macOS event tap for hotkeys");
            return Task.FromResult(false);
        }

        var runLoopSource = NativeMethods.CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
        _runLoopTask = Task.Run(() =>
        {
            _runLoop = NativeMethods.CFRunLoopGetCurrent();
            NativeMethods.CFRunLoopAddSource(_runLoop, runLoopSource, NativeMethods.CFRunLoopModeCommon);
            NativeMethods.CGEventTapEnable(_eventTap, true);
            NativeMethods.CFRunLoopRun();
        }, ct);

        return Task.FromResult(true);
    }

    public Task UnregisterAsync(CancellationToken ct)
    {
        UnregisterInternal();
        return Task.CompletedTask;
    }

    private IntPtr EventTapCallback(IntPtr proxy, NativeMethods.CGEventType type, IntPtr cgEvent, IntPtr refcon)
    {
        if (type == NativeMethods.CGEventType.KeyDown && _onTriggered != null)
        {
            var flags = (CGEventFlags)NativeMethods.CGEventGetFlags(cgEvent);
            if (ModifiersMatch(flags) && KeyMatches(cgEvent))
            {
                _logger.LogDebug("macOS global hotkey fired");
                _onTriggered();
            }
        }

        return cgEvent;
    }

    private bool KeyMatches(IntPtr cgEvent)
    {
        if (string.IsNullOrWhiteSpace(_targetKey))
        {
            return false;
        }

        Span<ushort> buffer = stackalloc ushort[4];
        buffer.Clear();
        var actualLength = 0;
        NativeMethods.CGEventKeyboardGetUnicodeString(cgEvent, buffer.Length, ref actualLength, ref MemoryMarshal.GetReference(buffer));

        if (actualLength == 0)
        {
            return false;
        }

        var pressed = char.ConvertFromUtf32(buffer[0]).Trim();
        return string.Equals(pressed, _targetKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool ModifiersMatch(CGEventFlags flags)
    {
        bool Requires(CGEventFlags flag) => (_targetModifiers & flag) != 0;

        if (Requires(CGEventFlags.MaskCommand) && !flags.HasFlag(CGEventFlags.MaskCommand))
        {
            return false;
        }

        if (Requires(CGEventFlags.MaskControl) && !flags.HasFlag(CGEventFlags.MaskControl))
        {
            return false;
        }

        if (Requires(CGEventFlags.MaskShift) && !flags.HasFlag(CGEventFlags.MaskShift))
        {
            return false;
        }

        if (Requires(CGEventFlags.MaskAlternate) && !flags.HasFlag(CGEventFlags.MaskAlternate))
        {
            return false;
        }

        return true;
    }

    private static CGEventFlags ParseModifiers(string modifiers)
    {
        var flags = CGEventFlags.None;
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return flags;
        }

        var parts = modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.Equals("cmd", StringComparison.OrdinalIgnoreCase) || part.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                flags |= CGEventFlags.MaskCommand;
            }
            else if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("control", StringComparison.OrdinalIgnoreCase))
            {
                flags |= CGEventFlags.MaskControl;
            }
            else if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                flags |= CGEventFlags.MaskShift;
            }
            else if (part.Equals("alt", StringComparison.OrdinalIgnoreCase) || part.Equals("option", StringComparison.OrdinalIgnoreCase))
            {
                flags |= CGEventFlags.MaskAlternate;
            }
        }

        return flags;
    }

    private void UnregisterInternal()
    {
        try
        {
            if (_runLoop != IntPtr.Zero)
            {
                NativeMethods.CFRunLoopStop(_runLoop);
            }

            if (_eventTap != IntPtr.Zero)
            {
                NativeMethods.CGEventTapEnable(_eventTap, false);
                _eventTap = IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop macOS event tap");
        }
    }

    public void Dispose()
    {
        UnregisterInternal();
    }

    [Flags]
    private enum CGEventFlags : ulong
    {
        None = 0,
        MaskShift = 1 << 17,
        MaskControl = 1 << 18,
        MaskAlternate = 1 << 19,
        MaskCommand = 1 << 20,
    }

    private delegate IntPtr CGEventTapCallback(IntPtr proxy, NativeMethods.CGEventType type, IntPtr cgEvent, IntPtr refcon);

    private static class NativeMethods
    {
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

        public const string CFRunLoopModeCommon = "kCFRunLoopCommonModes";

        public enum CGEventTapLocation : uint
        {
            Hid = 0,
            Session = 1,
            AnnotatedSession = 2,
        }

        public enum CGEventTapPlacement : uint
        {
            HeadInsert = 0,
            TailAppend = 1,
        }

        [Flags]
        public enum CGEventTapOptions : uint
        {
            Default = 0,
            ListenOnly = 1,
        }

        public enum CGEventType : uint
        {
            Null = 0,
            KeyDown = 10,
        }

        [DllImport(ApplicationServices)]
        public static extern bool CGPreflightListenEventAccess();

        [DllImport(CoreGraphics)]
        public static extern IntPtr CGEventTapCreate(CGEventTapLocation tap, CGEventTapPlacement place, CGEventTapOptions options, ulong eventsOfInterest, CGEventTapCallback callback, IntPtr userInfo);

        [DllImport(CoreGraphics)]
        public static extern ulong CGEventMaskBit(CGEventType type);

        [DllImport(CoreGraphics)]
        public static extern void CGEventTapEnable(IntPtr tap, bool enable);

        [DllImport(CoreGraphics)]
        public static extern ulong CGEventGetFlags(IntPtr cgEvent);

        [DllImport(CoreGraphics)]
        public static extern void CGEventKeyboardGetUnicodeString(IntPtr cgEvent, int maxStringLength, ref int actualStringLength, ref ushort unicodeString);

        [DllImport(CoreGraphics)]
        public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr tap, int order);

        [DllImport(CoreGraphics)]
        public static extern IntPtr CFRunLoopGetCurrent();

        [DllImport(CoreGraphics)]
        public static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, string mode);

        [DllImport(CoreGraphics)]
        public static extern void CFRunLoopRun();

        [DllImport(CoreGraphics)]
        public static extern void CFRunLoopStop(IntPtr rl);
    }
}
