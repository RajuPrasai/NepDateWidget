using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

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
    // Common legacy Nepali font name fragments (all lowercase for comparison).
    private static readonly string[] _legacyNepaliFonts =
        ["preeti", "kantipur", "himalaya", "sagarmatha", "sabdatara", "nagarjuna", "shangrila", "pcs"];

    public static bool IsLegacyNepaliFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return false;
        var lower = fontName.ToLowerInvariant();
        foreach (var prefix in _legacyNepaliFonts)
            if (lower.Contains(prefix)) return true;
        return false;
    }

    /// <summary>
    /// Returns the output path: same directory and stem as the input, with
    /// "_converted" appended before the extension. Extension is unchanged.
    /// </summary>
    public static string BuildOutputPath(string inputPath)
    {
        var dir  = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var ext  = Path.GetExtension(inputPath);
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
            text = sr.ReadToEnd();
        // font = null → caller converts unconditionally
        string converted = transform(text, null);
        File.WriteAllText(outputPath, converted, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // ── .docx (DocumentFormat.OpenXml) ───────────────────────────────────────

    private static void ConvertDocx(
        string inputPath,
        string outputPath,
        Func<string, string?, string> transform,
        Func<string?, string?>? fontMapper)
    {
        // Work on a copy so the original is never touched.
        File.Copy(inputPath, outputPath, overwrite: true);

        using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);

        var main = doc.MainDocumentPart;
        if (main is null) return;

        // Body
        if (main.Document?.Body is { } body)
            ProcessOpenXmlTexts(body.Descendants<Text>(), transform, fontMapper);

        // Headers
        foreach (var hp in main.HeaderParts)
        {
            if (hp.Header is { } header)
                ProcessOpenXmlTexts(header.Descendants<Text>(), transform, fontMapper);
        }

        // Footers
        foreach (var fp in main.FooterParts)
        {
            if (fp.Footer is { } footer)
                ProcessOpenXmlTexts(footer.Descendants<Text>(), transform, fontMapper);
        }

        doc.Save();
    }

    private static void ProcessOpenXmlTexts(
        IEnumerable<Text> elements,
        Func<string, string?, string> transform,
        Func<string?, string?>? fontMapper)
    {
        foreach (var textEl in elements)
        {
            string original = textEl.Text ?? string.Empty;
            if (original.Length == 0) continue;

            // Resolve font name from the parent Run's properties.
            string? fontName = null;
            Run? run = null;
            if (textEl.Parent is Run r)
            {
                run = r;
                var rf = run.RunProperties?.RunFonts;
                fontName = rf?.Ascii?.Value
                        ?? rf?.HighAnsi?.Value
                        ?? rf?.EastAsia?.Value
                        ?? rf?.ComplexScript?.Value
                        ?? string.Empty;  // explicitly "" = inherit (not null = no-info)
            }

            string converted = transform(original, fontName);
            if (converted == original) continue;

            textEl.Text = converted;

            // Update the run font when the text encoding changed.  fontMapper returns:
            //   null          → leave font unchanged
            //   string.Empty  → remove explicit run font (inherits document/style default)
            //   "FontName"    → set Ascii + HighAnsi to that font
            if (run is not null && fontMapper is not null)
            {
                string? newFont = fontMapper(fontName);
                if (newFont is not null)
                    ApplyRunFont(run, newFont);
            }
        }
    }

    private static void ApplyRunFont(Run run, string font)
    {
        var rp = run.RunProperties;
        if (rp == null)
        {
            rp = new RunProperties();
            run.PrependChild(rp);
        }

        if (font.Length == 0)
        {
            // Remove the explicit RunFonts element so the run inherits the document default.
            // After a P2U conversion the legacy font declaration is no longer correct;
            // modern Word will apply appropriate Unicode/Devanagari font fallback automatically.
            rp.RunFonts?.Remove();
        }
        else
        {
            var rf = rp.RunFonts;
            if (rf == null)
            {
                rf = new RunFonts();
                rp.PrependChild(rf);
            }
            rf.Ascii   = font;
            rf.HighAnsi = font;
        }
    }
}

