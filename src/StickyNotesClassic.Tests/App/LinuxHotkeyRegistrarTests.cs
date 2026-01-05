using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StickyNotesClassic.App.Services.Hotkeys;
using Xunit;

namespace StickyNotesClassic.Tests.App;

public class LinuxHotkeyRegistrarTests
{
    [Fact]
    public async Task RegisterAsync_ReturnsFalse_WhenDisplayUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var registrar = new LinuxHotkeyRegistrar(NullLogger<LinuxHotkeyRegistrar>.Instance);

        var registered = await registrar.RegisterAsync("Control,Alt", "N", () => { }, CancellationToken.None);

        Assert.False(registered);
        Assert.NotNull(registrar.UnsupportedReason);
    }
}
