using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StickyNotesClassic.App.Services.Hotkeys;

/// <summary>
/// Windows implementation of global hotkey registration using a low-level keyboard hook.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;

    private readonly ILogger<WindowsHotkeyRegistrar> _logger;
    private NativeMethods.LowLevelKeyboardProc? _hookCallback;
    private IntPtr _hookId;
    private bool _isRegistered;
    private uint _targetKey;
    private ModifierState _targetModifiers;
    private Action? _onTriggered;

    public WindowsHotkeyRegistrar(ILogger<WindowsHotkeyRegistrar> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public string? UnsupportedReason => IsSupported ? null : "Requires Windows low-level keyboard hooks.";

    public Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct)
    {
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        UnregisterInternal();

        _targetModifiers = ParseModifiers(modifiers);
        _targetKey = ParseKeyCode(key);
        _onTriggered = onTriggered;

        _hookCallback = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(WhKeyboardLl, _hookCallback, NativeMethods.GetModuleHandle(IntPtr.Zero), 0);

        _isRegistered = _hookId != IntPtr.Zero;

        if (!_isRegistered)
        {
            _logger.LogWarning("Failed to register global hotkey hook: {Error}", Marshal.GetLastWin32Error());
        }

        return Task.FromResult(_isRegistered);
    }

    public Task UnregisterAsync(CancellationToken ct)
    {
        UnregisterInternal();
        return Task.CompletedTask;
    }

    private void UnregisterInternal()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _isRegistered = false;
        _onTriggered = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WmKeydown)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var modifiersMatch = ModifiersMatch();
            var keyMatch = hookStruct.vkCode == _targetKey;

            if (_isRegistered && modifiersMatch && keyMatch)
            {
                _logger.LogDebug("Global hotkey fired: Modifiers={Modifiers}, Key={Key}", _targetModifiers, _targetKey);
                _onTriggered?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static uint ParseKeyCode(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        var trimmed = key.Trim();
        if (trimmed.Length == 1)
        {
            return (uint)char.ToUpperInvariant(trimmed[0]);
        }

        if (Enum.TryParse(trimmed, true, out ConsoleKey consoleKey))
        {
            return (uint)consoleKey;
        }

        return 0;
    }

    private static ModifierState ParseModifiers(string modifiers)
    {
        var state = ModifierState.None;
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            return state;
        }

        foreach (var mod in modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (mod.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                mod.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                state |= ModifierState.Control;
            }
            else if (mod.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                state |= ModifierState.Alt;
            }
            else if (mod.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                state |= ModifierState.Shift;
            }
        }

        return state;
    }

    private static bool ModifiersMatch(ModifierState target, Func<int, bool> keyState)
    {
        if (target.HasFlag(ModifierState.Control) && !keyState(VkLControl) && !keyState(VkRControl))
        {
            return false;
        }

        if (target.HasFlag(ModifierState.Alt) && !keyState(VkLMenu) && !keyState(VkRMenu))
        {
            return false;
        }

        if (target.HasFlag(ModifierState.Shift) && !keyState(VkLShift) && !keyState(VkRShift))
        {
            return false;
        }

        return true;
    }

    private bool ModifiersMatch()
    {
        return ModifiersMatch(_targetModifiers, code => (NativeMethods.GetAsyncKeyState(code) & 0x8000) != 0);
    }

    public void Dispose()
    {
        UnregisterInternal();
    }

    [Flags]
    private enum ModifierState
    {
        None = 0,
        Control = 1,
        Alt = 2,
        Shift = 4
    }

    private static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(IntPtr lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        public struct KbdLlHookStruct
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public nint dwExtraInfo;
        }
    }
}
