using System;

namespace StickyNotesClassic.App.Diagnostics;

public static class DebugOptions
{
    public static bool ShowHitTestOverlay =>
        string.Equals(Environment.GetEnvironmentVariable("STICKY_NOTES_HITTEST_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);
}
