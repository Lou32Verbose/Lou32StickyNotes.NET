using FluentAssertions;
using StickyNotesClassic.Core.Models;
using Xunit;

namespace StickyNotesClassic.Tests.Core.Models;

public class NoteTests
{
    [Fact]
    public void CreateNew_ShouldGenerateUniqueId()
    {
        // Arrange & Act
        var note1 = Note.CreateNew();
        var note2 = Note.CreateNew();
        
        // Assert
        note1.Id.Should().NotBe(note2.Id);
        note1.Id.Should().NotBeNullOrEmpty();
        note2.Id.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void CreateNew_ShouldSetDefaultDimensions()
    {
        // Act
        var note = Note.CreateNew();
        
        // Assert
        note.Width.Should().Be(Note.DefaultWidth);
        note.Height.Should().Be(Note.DefaultHeight);
    }
    
    [Fact]
    public void CreateNew_ShouldSetDefaultColor()
    {
        // Act
        var note = Note.CreateNew();
        
        // Assert
        note.Color.Should().Be(NoteColor.Yellow);
    }
    
    [Fact]
    public void CreateNew_WithColor_ShouldUseProvidedColor()
    {
        // Act
        var note = Note.CreateNew(NoteColor.Blue);
        
        // Assert
        note.Color.Should().Be(NoteColor.Blue);
    }
    
    [Fact]
    public void CreateNew_ShouldNotBeDeleted()
    {
        // Act
        var note = Note.CreateNew();
        
        // Assert
        note.IsDeleted.Should().BeFalse();
    }
    
    [Fact]
    public void CreateNew_ShouldNotBeTopmost()
    {
        // Act
        var note = Note.CreateNew();
        
        // Assert
        note.IsTopmost.Should().BeFalse();
    }
    
    [Fact]
    public void CreateNew_ShouldHaveEmptyContent()
    {
        // Act
        var note = Note.CreateNew();
        
        // Assert
        note.ContentRtf.Should().BeEmpty();
        note.ContentText.Should().BeEmpty();
    }
    
    [Fact]
    public void ValidateBounds_ShouldClampWidthToMinimum()
    {
        // Arrange
        var note = new Note { Width = 50, Height = 200 };
        
        // Act
        note.ValidateBounds();
        
        // Assert
        note.Width.Should().Be(Note.MinWidth);
    }
    
    [Fact]
    public void ValidateBounds_ShouldClampHeightToMinimum()
    {
        // Arrange
        var note = new Note { Width = 200, Height = 50 };
        
        // Act
        note.ValidateBounds();
        
        // Assert
        note.Height.Should().Be(Note.MinHeight);
    }
    
    [Fact]
    public void ValidateBounds_ShouldNotChangeValidDimensions()
    {
        // Arrange
        var note = new Note { Width = 300, Height = 300 };
        
        // Act
        note.ValidateBounds();
        
        // Assert
        note.Width.Should().Be(300);
        note.Height.Should().Be(300);
    }
}
