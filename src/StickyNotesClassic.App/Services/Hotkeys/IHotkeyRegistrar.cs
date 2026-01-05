using System;
using System.Threading;
using System.Threading.Tasks;

namespace StickyNotesClassic.App.Services.Hotkeys;

/// <summary>
/// Abstraction for registering a platform-specific global hotkey.
/// </summary>
public interface IHotkeyRegistrar : IDisposable
{
    /// <summary>
    /// True when the current platform supports registering a global hotkey.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Optional explanation when the registrar is unavailable on the current platform.
    /// </summary>
    string? UnsupportedReason { get; }

    /// <summary>
    /// Registers a global hotkey and wires the provided callback.
    /// </summary>
    /// <param name="modifiers">Comma-separated modifiers (Control, Alt, Shift).</param>
    /// <param name="key">The key portion (single character or key name).</param>
    /// <param name="onTriggered">Callback executed when the hotkey fires.</param>
    /// <param name="ct">Cancellation token used to abort registration.</param>
    Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct);

    /// <summary>
    /// Unregisters any previously configured hotkey.
    /// </summary>
    Task UnregisterAsync(CancellationToken ct);
}
