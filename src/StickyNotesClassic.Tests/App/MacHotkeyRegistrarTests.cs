using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StickyNotesClassic.App.Services.Hotkeys;
using Xunit;

#pragma warning disable CA1416

namespace StickyNotesClassic.Tests.App;

public class MacHotkeyRegistrarTests
{
    [Fact]
    public async Task Should_report_unsupported_off_macOS()
    {
        var registrar = new MacHotkeyRegistrar(new NullLogger<MacHotkeyRegistrar>());
        if (OperatingSystem.IsMacOS())
        {
            return; // cannot validate support on macOS in CI
        }

        var result = await registrar.RegisterAsync("cmd+shift", "N", () => { }, CancellationToken.None);
        result.Should().BeFalse();
        registrar.UnsupportedReason.Should().NotBeNull();
    }
}
