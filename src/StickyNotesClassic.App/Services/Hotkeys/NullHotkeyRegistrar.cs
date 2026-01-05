using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StickyNotesClassic.App.Services.Hotkeys;

/// <summary>
/// No-op registrar used on platforms that do not support global hotkeys.
/// </summary>
public sealed class NullHotkeyRegistrar : IHotkeyRegistrar
{
    private readonly ILogger<NullHotkeyRegistrar> _logger;

    public NullHotkeyRegistrar(ILogger<NullHotkeyRegistrar> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => false;

    public string? UnsupportedReason => "Global hotkeys are unavailable on this platform.";

    public Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct)
    {
        _logger.LogInformation("Global hotkeys are not supported on this platform.");
        return Task.FromResult(false);
    }

    public Task UnregisterAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
    }
}
