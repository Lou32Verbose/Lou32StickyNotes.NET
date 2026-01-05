using System.Text;

namespace StickyNotesClassic.Core.Utilities;

/// <summary>
/// Helpers for generating and normalizing RTF payloads used by the note editor.
/// </summary>
public static class RtfHelper
{
    /// <summary>
    /// Builds a minimal RTF document for the provided plain text and font settings.
    /// </summary>
    public static string BuildRtf(string text, string fontFamily, double fontSize)
    {
        var sb = new StringBuilder();
        var escapedText = EscapeRtf(text);
        var sizeInHalfPoints = (int)Math.Round(fontSize * 2);

        sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 ").Append(fontFamily).Append(@";}}{");
        sb.Append(@"\colortbl ;}\viewkind4\uc1\pard\f0\fs").Append(sizeInHalfPoints).Append(' ');
        sb.Append(escapedText).Append(@"\par}");

        return sb.ToString();
    }

    /// <summary>
    /// Ensures content is represented as valid RTF, creating a minimal document when the
    /// provided payload is missing or invalid.
    /// </summary>
    public static string EnsureRtf(string? rtf, string fallbackText, string fontFamily, double fontSize)
    {
        if (!string.IsNullOrWhiteSpace(rtf) && rtf.TrimStart().StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
        {
            return rtf;
        }

        return BuildRtf(fallbackText, fontFamily, fontSize);
    }

    private static string EscapeRtf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("\r\n", "\\par ")
            .Replace("\n", "\\par ")
            .Replace("\r", "\\par ");

        return escaped;
    }
}
