using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.App.Services.Hotkeys;
using StickyNotesClassic.Core.Models;
using Xunit;

namespace StickyNotesClassic.Tests.App;

public class HotkeyServiceTests
{
    [Fact]
    public async Task TryRegisterAsync_ReturnsFalse_WhenRegistrarUnsupported()
    {
        using var registrar = new TestRegistrar(isSupported: false);
        using var service = new HotkeyService(registrar, NullLogger<HotkeyService>.Instance);

        var result = await service.TryRegisterAsync(new AppSettings { HotkeyKey = "N", HotkeyModifiers = "Control" }, CancellationToken.None);

        Assert.False(result);
        Assert.False(registrar.RegisterCalled);
    }

    [Fact]
    public async Task HotkeyPressed_Raised_WhenCallbackInvoked()
    {
        using var registrar = new TestRegistrar(isSupported: true);
        using var service = new HotkeyService(registrar, NullLogger<HotkeyService>.Instance);
        var raised = false;
        service.HotkeyPressed += (_, _) => raised = true;

        var result = await service.TryRegisterAsync(new AppSettings { HotkeyKey = "N", HotkeyModifiers = "Control" }, CancellationToken.None);
        Assert.True(result);

        registrar.Trigger();

        Assert.True(raised);
    }

    private sealed class TestRegistrar : IHotkeyRegistrar
    {
        private Action? _callback;

        public TestRegistrar(bool isSupported)
        {
            IsSupported = isSupported;
        }

        public bool RegisterCalled { get; private set; }

        public bool IsSupported { get; }

        public string? UnsupportedReason => IsSupported ? null : "unsupported";

        public Task<bool> RegisterAsync(string modifiers, string key, Action onTriggered, CancellationToken ct)
        {
            RegisterCalled = true;
            _callback = onTriggered;
            return Task.FromResult(IsSupported);
        }

        public Task UnregisterAsync(CancellationToken ct)
        {
            _callback = null;
            return Task.CompletedTask;
        }

        public void Trigger() => _callback?.Invoke();

        public void Dispose()
        {
        }
    }
}
