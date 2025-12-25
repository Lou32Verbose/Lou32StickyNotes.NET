using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Service for registering global hotkeys.
/// Windows implementation uses P/Invoke to Win32 APIs.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID = 9000;
    private IntPtr _windowHandle;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    /// <summary>
    /// Registers a global hotkey.
    /// </summary>
    /// <param name="windowHandle">Window handle to receive hotkey messages</param>
    /// <param name="modifiers">Modifier keys (e.g., "Control,Alt")</param>
    /// <param name="key">Key character (e.g., "N")</param>
    [SupportedOSPlatform("windows")]
    public bool RegisterHotkey(IntPtr windowHandle, string modifiers, string key)
    {
        if (string.IsNullOrWhiteSpace(modifiers) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        _windowHandle = windowHandle;

        // Parse modifiers
        uint modifierFlags = 0;
        var modifierParts = modifiers.Split(',');
        foreach (var mod in modifierParts)
        {
            var trimmed = mod.Trim();
            if (trimmed.Equals("Control", StringComparison.OrdinalIgnoreCase))
                modifierFlags |= MOD_CONTROL;
            else if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                modifierFlags |= MOD_ALT;
            else if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                modifierFlags |= MOD_SHIFT;
        }

        // Parse key (take first character, convert to uppercase)
        var keyChar = key.Trim().ToUpper()[0];
        uint vkCode = (uint)keyChar;

        // Register the hotkey
        try
        {
            _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, modifierFlags, vkCode);
            return _isRegistered;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register hotkey: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unregisters the global hotkey.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void UnregisterHotkey()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    /// <summary>
    /// Processes window messages to detect hotkey press.
    /// Call this from your window's message handler.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void ProcessMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        const int WM_HOTKEY = 0x0312;
        
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            UnregisterHotkey();
        }
    }

    // Windows P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier constants
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
}
