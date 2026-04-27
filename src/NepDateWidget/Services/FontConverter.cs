using System;
using System.Text;

namespace NepDateWidget.Services;

/// <summary>
/// Converts text between Preeti font encoding and Unicode (Devanagari).
/// All mapping arrays are static to avoid repeated allocation.
/// </summary>
public static class FontConverter
{
    private static readonly string[] _preetiSource =
    {
        "ç", "˜", ".", "'m", "]m", "Fmf", "Fm", "=", "é",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "k|m", "em", "km", "Qm", "qm", "N˜",
        "¡", "¢", "!", "@", "$", ">", "?", "B", "I", "Q", "ß",
        "q", "„", "‹", "•", "›", "§", "°", "¶", "¿", "Å",
        "Ë", "Ì", "Í", "Î", "Ý", "å",
        "^«", "&«", "*«", "(«",
        "Ø", "|",
        "8Þ", "9Þ",
        "S", "s", "V", "v", "U", "u", "£", "#", "ª",
        "R", "r", "%", "H", "h", "‰", "´", "~", "`",
        "^", "&", "*", "(", ")",
        "T", "t", "Y", "y", "b", "W", "w", "G", "g",
        "K", "k", "ˆ", "A", "a", "E", "e", "D", "d",
        "o", "/", "N", "n", "J", "j", "Z", "z", "i", ":", ";", "X", "x",
        "cf'", "c'f", "cf}", "cf]", "cf", "c", "O{", "O", "pm", "p", "C", "P]", "P",
        "f'", "\"", "'", "+", "f", "[", "\\", "]", "}", "F", "L", "M",
        "्ा", "्ो", "्ौ", "अो", "अा", "आै", "आे", "ाो", "ाॅ", "ाे",
        "ंु", "ेे", "अै", "ाे", "अे", "ंा", "अॅ", "ाै", "ैा", "ंृ",
        "ँा", "ँू", "ेा", "ंे"
    };

    private static readonly string[] _unicodeTarget =
    {
        "ॐ", "ऽ", "।", "m'", "m]", "mfF", "mF", ".", "ङ्ग",
        "०", "१", "२", "३", "४", "५", "६", "७", "८", "९",
        "फ्र", "झ", "फ", "क्त", "क्र", "ल",
        "ज्ञ्", "द्घ", "ज्ञ", "द्द", "द्ध", "श्र", "रु", "द्य", "क्ष्", "त्त", "द्म",
        "त्र", "ध्र", "ङ्घ", "ड्ड", "द्र", "ट्ट", "ड्ढ", "ठ्ठ", "रू", "हृ",
        "ङ्ग", "त्र", "ङ्क", "ङ्ख", "ट्ठ", "द्व",
        "ट्र", "ठ्र", "ड्र", "ढ्र",
        "्य", "्र",
        "ड़", "ढ़",
        "क्", "क", "ख्", "ख", "ग्", "ग", "घ्", "घ", "ङ",
        "च्", "च", "छ", "ज्", "ज", "झ्", "झ", "ञ्", "ञ",
        "ट", "ठ", "ड", "ढ", "ण्",
        "त्", "त", "थ्", "थ", "द", "ध्", "ध", "न्", "न",
        "प्", "प", "फ्", "ब्", "ब", "भ्", "भ", "म्", "म",
        "य", "र", "ल्", "ल", "व्", "व", "श्", "श", "ष्", "स्", "स", "ह्", "ह",
        "ऑ", "ऑ", "औ", "ओ", "आ", "अ", "ई", "इ", "ऊ", "उ", "ऋ", "ऐ", "ए",
        "ॉ", "ू", "ु", "ं", "ा", "ृ", "्", "े", "ै", "ँ", "ी", "ः",
        "", "े", "ै", "ओ", "आ", "औ", "ओ", "ो", "ॉ", "ो",
        "ुं", "े", "अ‍ै", "ो", "अ‍े", "ां", "अ‍ॅ", "ौ", "ौ", "ृं",
        "ाँ", "ूँ", "ो", "ें"
    };

    private static readonly string[] _unicodeSource =
    {
        "m'", "m]", "mfF", "mF",
        "ॐ", "ऽ", "।",
        "०", "१", "२", "३", "४", "५", "६", "७", "८", "९",
        "ज्ञ्", "क्ष्", "फ्र", "द्घ", "ज्ञ", "द्द", "द्ध", "श्र", "क्त", "क्र", "द्य", "त्त", "द्म", "त्र", "ध्र", "ङ्घ", "ड्ड", "द्र", "ट्ट", "ड्ढ", "ठ्ठ", "रू", "हृ", "रु", "ङ्ग", "ङ्क", "ङ्ख", "ट्ठ", "द्व", "ट्र", "ठ्र", "ड्र", "ढ्र",
        "्य", "्र",
        "ड़", "ढ़",
        "ऑ", "औ", "ओ", "आ", "अ", "ई", "इ", "ऊ", "उ", "ऋ", "ऐ", "ए",
        "क्", "ख्", "ग्", "घ्", "च्", "ज्", "झ्", "ञ्", "ण्", "त्", "थ्", "ध्", "न्", "प्", "फ्", "ब्", "भ्", "म्", "ल्", "व्", "श्", "ष्", "स्", "ह्",
        "क", "ख", "ग", "घ", "ङ", "च", "छ", "ज", "झ", "ञ", "ट", "ठ", "ड", "ढ", "त", "थ", "द", "ध", "न", "प", "फ", "ब", "भ", "म", "य", "र", "ल", "व", "श", "स", "ह",
        "ॉ", "ू", "ु", "ं", "ा", "ृ", "्", "े", "ै", "ँ", "ी", "ः",
        "."
    };

    private static readonly string[] _preetiTarget =
    {
        "'m", "]m", "Fmf", "Fm",
        "ç", "˜", ".",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "¡", "I", "k|m", "¢", "!", "@", "$", ">", "Qm", "qm", "B", "Q", "ß", "q", "„", "‹", "•", "›", "§", "°", "¶", "¿", "Å", "?", "é", "Í", "Î", "Ý", "å", "^«", "&«", "*«", "(«",
        "Ø", "|",
        "8Þ", "9Þ",
        "cf'", "cf}", "cf]", "cf", "c", "O{", "O", "pm", "p", "C", "P]", "P",
        "S", "V", "U", "£", "R", "H", "‰", "~", ")", "T", "Y", "W", "G", "K", "ˆ", "A", "E", "D", "N", "J", "Z", "i", ":", "X",
        "s", "v", "u", "#", "ª", "r", "%", "h", "em", "`", "^", "&", "*", "(", "t", "y", "b", "w", "g", "k", "km", "a", "e", "d", "o", "/", "n", "j", "z", ";", "x",
        "f'", "\"", "'", "+", "f", "[", "\\", "]", "}", "F", "L", "M",
        "="
    };

    static FontConverter()
    {
        if (_preetiSource.Length != _unicodeTarget.Length)
            throw new InvalidOperationException("Preeti to Unicode mapping arrays must have the same length.");

        if (_unicodeSource.Length != _preetiTarget.Length)
            throw new InvalidOperationException("Unicode to Preeti mapping arrays must have the same length.");
    }

    public static string ConvertToUnicode(string preetiText)
    {
        if (string.IsNullOrWhiteSpace(preetiText))
            return string.Empty;

        string text = preetiText.Normalize(NormalizationForm.FormC);
        text = ApplyMappings(text, _preetiSource, _unicodeTarget);
        return text.Normalize(NormalizationForm.FormC);
    }

    public static string ConvertToPreeti(string unicodeText)
    {
        if (string.IsNullOrWhiteSpace(unicodeText))
            return string.Empty;

        string text = unicodeText.Normalize(NormalizationForm.FormC);
        text = ReorderPreposedIMatra(text);
        text = ReorderReph(text);
        text = ApplyMappings(text, _unicodeSource, _preetiTarget);
        return text;
    }

    private static string ApplyMappings(string text, string[] sources, string[] targets)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            string source = sources[i];
            string target = targets[i];

            if (string.IsNullOrEmpty(source))
                continue;

            text = ReplaceAllSequentially(text, source, target);
        }

        return text;
    }

    private static string ReplaceAllSequentially(string text, string source, string target)
    {
        int index = text.IndexOf(source, StringComparison.Ordinal);

        while (index >= 0)
        {
            text = text.Remove(index, source.Length).Insert(index, target);
            index = text.IndexOf(source, index + target.Length, StringComparison.Ordinal);
        }

        return text;
    }

    private static string ReorderPreposedIMatra(string text)
    {
        const char iMatra = '\u093F';   // ि
        const char halant = '\u094D';   // ्

        int pos = text.IndexOf(iMatra);

        while (pos > 0)
        {
            char leftChar = text[pos - 1];

            text = text.Remove(pos - 1, 2).Insert(pos - 1, "l" + leftChar);
            pos -= 1;

            while (pos > 0 && text[pos - 1] == halant)
            {
                if (pos < 2)
                    break;

                char consonant = text[pos - 2];
                text = text.Remove(pos - 2, 3).Insert(pos - 2, "l" + consonant + halant);
                pos -= 2;
            }

            pos = text.IndexOf(iMatra, pos + 1);
        }

        return text;
    }

    private static string ReorderReph(string text)
    {
        const string reph = "र्";
        const char halant = '\u094D';   // ्
        const string matras = "ािीुूृेैोौं:ँॅ";

        text += "  ";

        int pos = text.IndexOf(reph, StringComparison.Ordinal);

        while (pos > 0)
        {
            int start = pos + reph.Length;
            int end = start;

            while (end < text.Length && IsRephFollower(text[end], matras))
                end++;

            int rightPos = end + 1;
            while (rightPos < text.Length && text[rightPos] == halant)
            {
                end = rightPos + 1;
                rightPos = end + 1;
            }

            int segmentLength = end - start;
            if (segmentLength <= 0)
                break;

            string segment = text.Substring(start, segmentLength);
            text = text.Remove(pos, reph.Length + segmentLength).Insert(pos, segment + "{");

            pos = text.IndexOf(reph, pos + segment.Length + 1, StringComparison.Ordinal);
        }

        return text.Substring(0, text.Length - 2);
    }

    private static bool IsRephFollower(char c, string matras)
    {
        return matras.IndexOf(c) >= 0;
    }
}
