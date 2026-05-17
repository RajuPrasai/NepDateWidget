using NepDateWidget.Models;
using System.IO;

namespace NepDateWidget.Services;

public sealed class FileTypeService : IFileTypeService
{
    // Extension-only MIME detection - no file reads.
    // HEIC and HEIF both normalize to the same MIME type so mixed HEIC+HEIF
    // batches from the same iPhone are accepted, not rejected.
    private static readonly Dictionary<string, string> _mimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png",  "image/png"  },
        { ".webp", "image/webp" },
        { ".gif",  "image/gif"  },
        { ".tif",  "image/tiff" },
        { ".tiff", "image/tiff" },
        { ".bmp",  "image/bmp"  },
        { ".heic", "image/heif" },  // normalized - both map to same type
        { ".heif", "image/heif" },
        { ".avif", "image/avif" },
        { ".pdf",  "application/pdf" },
    };

    private static readonly HashSet<string> _imageMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "image/tiff", "image/bmp", "image/heif", "image/avif",
    };

    public string? GetMimeType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return _mimeMap.TryGetValue(extension, out var mime) ? mime : null;
    }

    public FileCategory GetCategory(string mimeType)
    {
        if (_imageMimes.Contains(mimeType))
        {
            return FileCategory.Image;
        }

        if (string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return FileCategory.Pdf;
        }

        return FileCategory.Unsupported;
    }

    public string? ValidateSameType(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            return null;
        }

        var mimes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in filePaths)
        {
            var ext = Path.GetExtension(path);
            var mime = GetMimeType(ext);
            if (mime is null)
            {
                return $"Unsupported file type: {ext}";
            }

            mimes.Add(mime);
        }

        if (mimes.Count > 1)
        {
            return "Mixed file types detected. Please select files of the same type.";
        }

        return null;
    }
}
