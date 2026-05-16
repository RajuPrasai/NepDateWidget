using NepDateWidget.Models;

namespace NepDateWidget.Services;

public interface IFileTypeService
{
    /// <summary>Returns the MIME type string for the given file extension (including the dot), or null if unsupported.</summary>
    string? GetMimeType(string extension);

    /// <summary>Returns the FileCategory for routing. Unsupported extensions return FileCategory.Unsupported.</summary>
    FileCategory GetCategory(string mimeType);

    /// <summary>
    /// Validates that all files in the list share the same MIME type.
    /// Returns null if valid. Returns a descriptive error message if mixed types are detected.
    /// </summary>
    string? ValidateSameType(IReadOnlyList<string> filePaths);
}
