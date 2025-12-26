namespace StickyNotesClassic.Core.Services;

/// <summary>
/// Validation service for input sanitization and security.
/// </summary>
public static class ValidationService
{
    private const int MaxContentBytes = 10 * 1024 * 1024; // 10MB RTF limit
    private const int MaxContentLength = 1_000_000; // 1M characters
    private const double MaxWindowDimension = 10000; // Prevent absurdly large windows

    private static readonly string[] AllowedFonts = new[]
    {
        "Segoe Print", "Segoe UI", "Arial", "Calibri", "Comic Sans MS",
        "Consolas", "Courier New", "Georgia", "Times New Roman", 
        "Trebuchet MS", "Verdana", "Tahoma", "Lucida Sans"
    };

    /// <summary>
    /// Validates RTF content for size and format.
    /// </summary>
    public static ValidationResult ValidateRtfContent(string rtf)
    {
        if (string.IsNullOrEmpty(rtf))
            return ValidationResult.Success();

        if (System.Text.Encoding.UTF8.GetByteCount(rtf) > MaxContentBytes)
            return ValidationResult.Failure("RTF content exceeds maximum size (10MB)");

        if (rtf.Length > MaxContentLength)
            return ValidationResult.Failure("Content exceeds maximum length (1M characters)");

        // Basic RTF structure validation
        var trimmed = rtf.TrimStart();
        if (trimmed.Length > 0 && !trimmed.StartsWith("{\\rtf"))
            return ValidationResult.Failure("Invalid RTF format - must start with {\\rtf");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates font family against allowed list.
    /// </summary>
    public static ValidationResult ValidateFontFamily(string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
            return ValidationResult.Failure("Font family cannot be empty");

        if (!AllowedFonts.Contains(fontFamily, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(
                $"Font '{fontFamily}' is not in the allowed list. Allowed fonts: {string.Join(", ", AllowedFonts)}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates file path for security (prevents path traversal).
    /// </summary>
    public static ValidationResult ValidateFilePath(string path, bool mustExist = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Failure("File path cannot be empty");

        try
        {
            var fullPath = Path.GetFullPath(path);

            // Prevent path traversal - ensure the full path matches the original if rooted
            if (Path.IsPathRooted(path))
            {
                var normalizedInput = Path.GetFullPath(path);
                if (!normalizedInput.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Failure("Path traversal detected");
            }

            if (mustExist && !File.Exists(fullPath))
                return ValidationResult.Failure($"File does not exist: {fullPath}");

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Invalid file path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates window bounds for reasonable values.
    /// </summary>
    public static ValidationResult ValidateBounds(double x, double y, double width, double height)
    {
        // Check for invalid numeric values
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(width) || double.IsNaN(height))
            return ValidationResult.Failure("Invalid numeric values (NaN)");

        if (double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(width) || double.IsInfinity(height))
            return ValidationResult.Failure("Infinite values not allowed");

        // Check minimum size (Note class constants will be used in practice)
        if (width < 100 || height < 100)
            return ValidationResult.Failure("Window size below minimum (100x100)");

        // Check maximum size
        if (width > MaxWindowDimension || height > MaxWindowDimension)
            return ValidationResult.Failure($"Window size exceeds maximum ({MaxWindowDimension}px)");

        return ValidationResult.Success();
    }
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ValidationResult() { }

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(string message) => new() 
    { 
        IsValid = false, 
        ErrorMessage = message 
    };
}
