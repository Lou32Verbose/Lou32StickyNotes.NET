using FluentAssertions;
using StickyNotesClassic.Core.Models;
using Xunit;

namespace StickyNotesClassic.Tests.Core.Models;

public class AppSettingsTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Act
        var settings = new AppSettings();
        
        // Assert
        settings.DefaultFontFamily.Should().Be("Segoe Print");
        settings.DefaultFontSize.Should().Be(12.0);
        settings.DefaultNoteColor.Should().Be(NoteColor.Yellow);
        settings.EnableBackgroundGradient.Should().BeTrue();
        settings.EnableEnhancedShadow.Should().BeTrue();
        settings.EnableGlossyHeader.Should().BeTrue();
        settings.EnableTextShadow.Should().BeFalse();
        settings.HotkeyModifiers.Should().Be("Control,Alt");
        settings.HotkeyKey.Should().Be("N");
        settings.AutoBackupEnabled.Should().BeTrue();
        settings.AutoBackupRetentionDays.Should().Be(7);
    }
    
    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var settings = new AppSettings();
        
        // Act
        settings.DefaultFontFamily = "Arial";
        settings.DefaultFontSize = 14.0;
        settings.DefaultNoteColor = NoteColor.Blue;
        settings.EnableBackgroundGradient = false;
        settings.AutoBackupRetentionDays = 30;
        
        // Assert
        settings.DefaultFontFamily.Should().Be("Arial");
        settings.DefaultFontSize.Should().Be(14.0);
        settings.DefaultNoteColor.Should().Be(NoteColor.Blue);
        settings.EnableBackgroundGradient.Should().BeFalse();
        settings.AutoBackupRetentionDays.Should().Be(30);
    }
}
