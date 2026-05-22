using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NepDateWidget.Services;

/// <summary>
/// Converts text content in .txt, .docx, and .doc files using a caller-supplied
/// character-level transform. Document structure and formatting are preserved.
///
/// The transform delegate signature is (string text, string? fontName) → string.
/// fontName is:
///   null  for .txt files (no font context - caller should convert unconditionally)
///   ""    for .docx/.doc runs that have no explicit font set (inherited)
///   name  for runs with an explicit font (e.g. "Preeti", "Times New Roman")
/// </summary>
public static class DocumentConverterService
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XmlWriterSettings _xmlWriterSettings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = false
    };

    // Common legacy Nepali font name fragments (all lowercase for comparison).
    private static readonly string[] _legacyNepaliFonts =
        ["preeti", "kantipur", "himalaya", "sagarmatha", "sabdatara", "nagarjuna", "shangrila", "pcs"];

    public static bool IsLegacyNepaliFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return false;
        }

        var lower = fontName.ToLowerInvariant();
        foreach (var prefix in _legacyNepaliFonts)
        {
            if (lower.Contains(prefix))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the output path: same directory and stem as the input, with
    /// "_converted" appended before the extension. Extension is unchanged.
    /// </summary>
    public static string BuildOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, stem + "_converted" + ext);
    }

    /// <summary>
    /// Routes the conversion to the correct format handler based on the file extension.
    /// Throws <see cref="NotSupportedException"/> for unrecognised extensions.
    /// <para>
    /// <paramref name="fontMapper"/> is optional and only applies to .docx files. When provided,
    /// it is called with the original run font name whenever a run's text was actually changed.
    /// Return <c>null</c> to leave the font unchanged, <c>""</c> (empty string) to remove the
    /// explicit run font so the document default is inherited, or a font name to set it explicitly.
    /// </para>
    /// </summary>
    public static void Convert(
        string inputPath,
        string outputPath,
        Func<string, string?, string> transform,
        Func<string?, string?>? fontMapper = null)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();
        switch (ext)
        {
            case ".txt":
                ConvertTxt(inputPath, outputPath, transform);
                break;
            case ".docx":
                ConvertDocx(inputPath, outputPath, transform, fontMapper);
                break;
            case ".doc":
                throw new NotSupportedException(".doc (Word 97-2003) is not supported. Please save as .docx and retry.");
            default:
                throw new NotSupportedException($"'{ext}' files are not supported. Use .txt or .docx.");
        }
    }

    // ── .txt ─────────────────────────────────────────────────────────────────

    private static void ConvertTxt(
        string inputPath,
        string outputPath,
        Func<string, string?, string> transform)
    {
        // BOM-aware reading: handles UTF-8 (with or without BOM), UTF-16 LE/BE (from Windows Notepad
        // "Save As Unicode"), and falls back to UTF-8 for files without a recognisable BOM.
        string text;
        using (var sr = new StreamReader(inputPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = sr.ReadToEnd();
        }
        // font = null → caller converts unconditionally
        string converted = transform(text, null);
        File.WriteAllText(outputPath, converted, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // ── .docx (ZipArchive + XDocument) ──────────────────────────────────────

    private static void ConvertDocx(
        string inputPath,
        string outputPath,
        Func<string, string?, string> transform,
        Func<string?, string?>? fontMapper)
    {
        // Work on a copy so the original is never touched.
        File.Copy(inputPath, outputPath, overwrite: true);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Update);

        // Collect target entries before modifying the archive.
        var targets = zip.Entries
            .Where(e => e.FullName == "word/document.xml"
                     || (e.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                     || (e.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var entry in targets)
        {
            XDocument xdoc;
            using (var s = entry.Open())
            {
                xdoc = XDocument.Load(s);
            }

            if (!ProcessXmlTexts(xdoc, transform, fontMapper))
            {
                continue;
            }

            // Replace the entry: delete then re-create with the same name.
            string entryName = entry.FullName;
            entry.Delete();
            var updated = zip.CreateEntry(entryName);
            using var outStream = updated.Open();
            using var writer = XmlWriter.Create(outStream, _xmlWriterSettings);
            xdoc.Save(writer);
        }
    }

    private static bool ProcessXmlTexts(
        XDocument xdoc,
        Func<string, string?, string> transform,
        Func<string?, string?>? fontMapper)
    {
        bool changed = false;

        foreach (var textEl in xdoc.Descendants(_w + "t").ToList())
        {
            string original = textEl.Value;
            if (original.Length == 0)
            {
                continue;
            }

            // Resolve font name from the parent w:r > w:rPr > w:rFonts.
            string? fontName = null;
            XElement? run = null;
            if (textEl.Parent?.Name == _w + "r")
            {
                run = textEl.Parent;
                var rFonts = run.Element(_w + "rPr")?.Element(_w + "rFonts");
                fontName = (string?)rFonts?.Attribute(_w + "ascii")
                        ?? (string?)rFonts?.Attribute(_w + "hAnsi")
                        ?? (string?)rFonts?.Attribute(_w + "eastAsia")
                        ?? (string?)rFonts?.Attribute(_w + "cs")
                        ?? string.Empty;  // explicitly "" = inherit (not null = no-info)
            }

            string converted = transform(original, fontName);
            if (converted == original)
            {
                continue;
            }

            textEl.Value = converted;
            changed = true;

            // Update the run font when the text encoding changed.  fontMapper returns:
            //   null          → leave font unchanged
            //   string.Empty  → remove explicit run font (inherits document/style default)
            //   "FontName"    → set w:ascii + w:hAnsi to that font
            if (run is not null && fontMapper is not null)
            {
                string? newFont = fontMapper(fontName);
                if (newFont is not null)
                {
                    ApplyRunFont(run, newFont);
                }
            }
        }

        return changed;
    }

    private static void ApplyRunFont(XElement run, string font)
    {
        var rPr = run.Element(_w + "rPr");
        if (rPr == null)
        {
            rPr = new XElement(_w + "rPr");
            run.AddFirst(rPr);
        }

        if (font.Length == 0)
        {
            // Remove the explicit font element so the run inherits the document default.
            // After a Preeti-to-Unicode conversion the legacy font declaration is wrong;
            // Word will apply the correct Devanagari font via Unicode fallback.
            rPr.Element(_w + "rFonts")?.Remove();
        }
        else
        {
            var rFonts = rPr.Element(_w + "rFonts");
            if (rFonts == null)
            {
                rFonts = new XElement(_w + "rFonts");
                rPr.AddFirst(rFonts);
            }
            rFonts.SetAttributeValue(_w + "ascii", font);
            rFonts.SetAttributeValue(_w + "hAnsi", font);
        }
    }
}

