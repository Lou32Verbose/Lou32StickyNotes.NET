using System.Diagnostics;
using Avalonia.Threading;
using AvRichTextBox;
using FluentAssertions;

namespace StickyNotesClassic.Tests.Ui;

public class NoteWindowSmokeTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact(Timeout = 15000)]
    public async Task RichEditor_can_initialize_document()
    {
        var editor = await Dispatcher.UIThread.InvokeAsync(() => new RichTextBox());

        await Dispatcher.UIThread.InvokeAsync(() => editor.NewDocument());

        var documentExists = await Dispatcher.UIThread.InvokeAsync(() => editor.FlowDocument != null);
        documentExists.Should().BeTrue();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact(Timeout = 15000)]
    public async Task RichEditor_constructs_within_expected_budget()
    {
        var watch = Stopwatch.StartNew();
        var editor = await Dispatcher.UIThread.InvokeAsync(() => new RichTextBox());
        watch.Stop();

        editor.Should().NotBeNull();
        watch.ElapsedMilliseconds.Should().BeLessThan(400);
    }
}
