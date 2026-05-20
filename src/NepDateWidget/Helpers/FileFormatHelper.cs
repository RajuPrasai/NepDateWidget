using System.IO;

namespace NepDateWidget.Helpers;

/// <summary>
/// Shared file-size utilities used by image-processing view models.
/// Centralises <c>FormatBytes</c> and <c>GetFileSizeBytes</c> so they are not
/// duplicated across CompressionViewModel, ResizeViewModel, ImageConverterViewModel,
/// and ImageToolsViewModel.
/// </summary>
internal static class FileFormatHelper
{
    public static long GetFileSizeBytes(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)          return "0 B";
        if (bytes < 1_024)       return $"{bytes} B";
        if (bytes < 1_048_576)   return $"{bytes / 1_024.0:F1} KB";
        if (bytes < 1_073_741_824) return $"{bytes / 1_048_576.0:F1} MB";
        return $"{bytes / 1_073_741_824.0:F2} GB";
    }
}
