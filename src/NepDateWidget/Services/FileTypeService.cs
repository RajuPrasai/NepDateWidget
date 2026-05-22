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
        // RAW camera formats
        { ".arw", "image/x-sony-arw"      },
        { ".cr2", "image/x-canon-cr2"     },
        { ".cr3", "image/x-canon-cr3"     },
        { ".dng", "image/x-adobe-dng"     },
        { ".nef", "image/x-nikon-nef"     },
        { ".orf", "image/x-olympus-orf"   },
        { ".raf", "image/x-fuji-raf"      },
        { ".rw2", "image/x-panasonic-rw2" },
        { ".erf", "image/x-epson-erf"     },
        { ".pef", "image/x-pentax-pef"    },
        { ".x3f", "image/x-sigma-x3f"     },
    };

    private static readonly HashSet<string> _imageMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "image/tiff", "image/bmp", "image/heif", "image/avif",
    };

    // RAW MIME types are intentionally NOT in _imageMimes - they route through
    // FileCategory.Raw so ImageToolsViewModel can apply the correct smart defaults.
    private static readonly HashSet<string> _rawMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/x-sony-arw", "image/x-canon-cr2", "image/x-canon-cr3",
        "image/x-adobe-dng", "image/x-nikon-nef", "image/x-olympus-orf",
        "image/x-fuji-raf", "image/x-panasonic-rw2", "image/x-epson-erf",
        "image/x-pentax-pef", "image/x-sigma-x3f",
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

        if (_rawMimes.Contains(mimeType))
        {
            return FileCategory.Raw;
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
