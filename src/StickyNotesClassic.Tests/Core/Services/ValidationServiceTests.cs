using FluentAssertions;
using StickyNotesClassic.Core.Services;
using Xunit;

namespace StickyNotesClassic.Tests.Core.Services;

public class ValidationServiceTests
{
    [Fact]
    public void ValidateRtfContent_EmptyString_ShouldBeValid()
    {
        // Act
        var result = ValidationService.ValidateRtfContent(string.Empty);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateRtfContent_ValidRtf_ShouldBeValid()
    {
        // Arrange
        var rtf = "{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Arial;}} Hello World}";
        
        // Act
        var result = ValidationService.ValidateRtfContent(rtf);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateRtfContent_InvalidFormat_ShouldFail()
    {
        // Arrange
        var notRtf = "This is plain text, not RTF";
        
        // Act
        var result = ValidationService.ValidateRtfContent(notRtf);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid RTF format");
    }
    
    [Fact]
    public void ValidateRtfContent_TooLarge_ShouldFail()
    {
        // Arrange - Create content larger than 10MB
        var largeRtf = "{\\rtf1 " + new string('x', 11 * 1024 * 1024) + "}";
        
        // Act
        var result = ValidationService.ValidateRtfContent(largeRtf);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum size");
    }
    
    [Theory]
    [InlineData("Segoe Print")]
    [InlineData("Segoe UI")]
    [InlineData("Arial")]
    [InlineData("Calibri")]
    [InlineData("Comic Sans MS")]
    public void ValidateFontFamily_AllowedFont_ShouldBeValid(string fontFamily)
    {
        // Act
        var result = ValidationService.ValidateFontFamily(fontFamily);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateFontFamily_DisallowedFont_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateFontFamily("HackerFont");
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not in the allowed list");
    }
    
    [Fact]
    public void ValidateFontFamily_EmptyString_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateFontFamily(string.Empty);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }
    
    [Fact]
    public void ValidateFilePath_ValidPath_ShouldBeValid()
    {
        // Arrange
        var path = "C:\\Temp\\test.json";
        
        // Act
        var result = ValidationService.ValidateFilePath(path);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateFilePath_EmptyPath_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateFilePath(string.Empty);
        
        // Assert
        result.IsValid.Should().BeFalse();
    }
    
    [Fact]
    public void ValidateBounds_ValidBounds_ShouldBeValid()
    {
        // Act
        var result = ValidationService.ValidateBounds(100, 100, 400, 300);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateBounds_NaN_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateBounds(double.NaN, 100, 400, 300);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NaN");
    }
    
    [Fact]
    public void ValidateBounds_Infinity_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateBounds(100, double.PositiveInfinity, 400, 300);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Infinite");
    }
    
    [Fact]
    public void ValidateBounds_TooSmall_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateBounds(100, 100, 50, 50);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("below minimum");
    }
    
    [Fact]
    public void ValidateBounds_TooLarge_ShouldFail()
    {
        // Act
        var result = ValidationService.ValidateBounds(100, 100, 15000, 300);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum");
    }
}
