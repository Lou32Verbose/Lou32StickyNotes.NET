using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.App.ViewModels;
using Xunit;

namespace StickyNotesClassic.Tests.App;

public class RestoreBackupViewModelTests
{
    [Fact]
    public void Should_sort_backups_descending_and_select_latest()
    {
        var older = new BackupSummary
        {
            FilePath = "/tmp/old.json",
            LastWriteTimeUtc = DateTime.UtcNow.AddDays(-1),
            NoteCount = 1,
            ChecksumPresent = true,
            ChecksumValid = true,
            SizeBytes = 100
        };

        var newer = new BackupSummary
        {
            FilePath = "/tmp/new.json",
            LastWriteTimeUtc = DateTime.UtcNow,
            NoteCount = 2,
            ChecksumPresent = true,
            ChecksumValid = false,
            SizeBytes = 200
        };

        var vm = new RestoreBackupViewModel(new List<BackupSummary> { older, newer });

        vm.Backups.Select(b => b.FilePath).Should().ContainInOrder(newer.FilePath, older.FilePath);
        vm.SelectedBackup!.FilePath.Should().Be(newer.FilePath);
        vm.Backups[0].Status.Should().Be("checksum mismatch");
        vm.Backups[1].Status.Should().Be("checksum ok");
    }
}
