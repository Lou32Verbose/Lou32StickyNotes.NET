using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using StickyNotesClassic.App;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(StickyNotesClassic.Tests.Ui.UiTestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StickyNotesClassic.Tests.Ui;

public static class UiTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<UiTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .With(new FontManagerOptions());
}

public class UiTestApp : StickyNotesClassic.App.App
{
    public override void Initialize()
    {
        // Skip loading application XAML to avoid production bootstrapping during headless tests.
    }

    public override void OnFrameworkInitializationCompleted()
    {
    }
}
