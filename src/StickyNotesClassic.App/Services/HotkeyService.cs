using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StickyNotesClassic.App.Services.Hotkeys;
using StickyNotesClassic.Core.Models;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Coordinates platform-specific global hotkey registration.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly ILogger<HotkeyService> _logger;
    private CancellationTokenSource? _registrationScope;

    public event EventHandler? HotkeyPressed;

    public HotkeyService(IHotkeyRegistrar registrar, ILogger<HotkeyService> logger)
    {
        _registrar = registrar;
        _logger = logger;
    }

    public bool IsSupported => _registrar.IsSupported;

    /// <summary>
    /// Attempts to register a global hotkey based on application settings.
    /// </summary>
    public async Task<bool> TryRegisterAsync(AppSettings settings, CancellationToken ct)
    {
        if (!_registrar.IsSupported)
        {
            _logger.LogInformation("Global hotkeys are not supported on this platform: {Reason}",
                _registrar.UnsupportedReason ?? "platform limitation");
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.HotkeyKey) || string.IsNullOrWhiteSpace(settings.HotkeyModifiers))
        {
            _logger.LogWarning("Hotkey settings are incomplete; skipping registration.");
            return false;
        }

        _registrationScope?.Cancel();
        _registrationScope?.Dispose();
        _registrationScope = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var registered = await _registrar.RegisterAsync(settings.HotkeyModifiers, settings.HotkeyKey, () =>
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }, _registrationScope.Token);

        _logger.LogInformation("Global hotkey registration {Result}", registered ? "succeeded" : "failed");
        return registered;
    }

    public Task UnregisterAsync(CancellationToken ct)
    {
        _registrationScope?.Cancel();
        _registrationScope?.Dispose();
        _registrationScope = null;
        return _registrar.UnregisterAsync(ct);
    }

    public void Dispose()
    {
        _registrationScope?.Cancel();
        _registrar.Dispose();
        _registrationScope?.Dispose();
    }
}
