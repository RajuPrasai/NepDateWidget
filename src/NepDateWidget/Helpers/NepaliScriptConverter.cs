namespace NepDateWidget.Helpers;

/// <summary>
/// Bidirectional Nepali script converter using a phonetic rule engine.
/// Roman → Devanagari: greedy longest-match, state-machine (afterConsonant flag).
/// Devanagari → Roman: character walk with lookahead for halant and matras.
///
/// Vowel mapping convention:
///   a  = inherent schwa (adds no matra after a consonant; produces अ standalone)
///   aa = long ā  (ā/ा)
///   i  = short i (इ/ि)    ii or ee = long ī (ई/ी)
///   u  = short u (उ/ु)    uu or oo = long ū (ऊ/ू)
///   e  = ए/े   ai = ऐ/ै   o = ओ/ो   au = औ/ौ
/// </summary>
public static class NepaliScriptConverter
{
    // ── Pattern table ─────────────────────────────────────────────────────────
    // Each entry: (roman, devanagari_consonant_or_null, full_vowel_or_null, matra_or_null)
    // If devanagari_consonant is non-null → it is a consonant (or pre-built conjunct chunk).
    // Otherwise full_vowel and matra are used.

    private sealed record Pattern(string Roman, string? Consonant, string? FullVowel, string? Matra);

    // Ordered: longest patterns first within each group; multi-char patterns before single-char.
    private static readonly Pattern[] Patterns =
    [
        // ── Pre-built consonant chunks (must precede their components) ─────────
        new("ksha", "क्ष",  null, null),
        new("gya",  "ज्ञ",  null, null),
        new("shr",  "श्र", null, null),
        new("chh",  "छ",   null, null),

        // ── Two-char consonant digraphs ───────────────────────────────────────
        new("kh",   "ख",   null, null),
        new("gh",   "घ",   null, null),
        new("ch",   "च",   null, null),
        new("jh",   "झ",   null, null),
        new("ny",   "ञ",   null, null),
        new("ng",   "ङ",   null, null),
        new("th",   "थ",   null, null),
        new("dh",   "ध",   null, null),
        new("ph",   "फ",   null, null),
        new("bh",   "भ",   null, null),
        new("sh",   "श",   null, null),

        // ── Two-char vowels (before single-char vowels) ───────────────────────
        new("aa",   null,  "आ",  "ा"),
        new("ii",   null,  "ई",  "ी"),
        new("ee",   null,  "ई",  "ी"),
        new("uu",   null,  "ऊ",  "ू"),
        new("oo",   null,  "ऊ",  "ू"),
        new("ai",   null,  "ऐ",  "ै"),
        new("au",   null,  "औ",  "ौ"),

        // ── Single-char vowels ────────────────────────────────────────────────
        // 'a' matra is empty string (inherent vowel - no visible mark needed)
        new("a",    null,  "अ",  ""),
        new("i",    null,  "इ",  "ि"),
        new("u",    null,  "उ",  "ु"),
        new("e",    null,  "ए",  "े"),
        new("o",    null,  "ओ",  "ो"),

        // ── Single-char consonants ────────────────────────────────────────────
        new("k",    "क",   null, null),
        new("g",    "ग",   null, null),
        new("j",    "ज",   null, null),
        new("t",    "त",   null, null),
        new("d",    "द",   null, null),
        new("n",    "न",   null, null),
        new("p",    "प",   null, null),
        new("b",    "ब",   null, null),
        new("m",    "म",   null, null),
        new("y",    "य",   null, null),
        new("r",    "र",   null, null),
        new("l",    "ल",   null, null),
        new("v",    "व",   null, null),
        new("w",    "व",   null, null),
        new("s",    "स",   null, null),
        new("h",    "ह",   null, null),
        new("f",    "फ",   null, null),
        new("z",    "ज",   null, null),
        new("c",    "क",   null, null),
        new("q",    "क",   null, null),
        new("x",    "क्ष", null, null),
    ];

    // ── Devanagari → Roman maps ───────────────────────────────────────────────

    private static readonly Dictionary<char, string> ConsonantMap = new()
    {
        ['क'] = "k",
        ['ख'] = "kh",
        ['ग'] = "g",
        ['घ'] = "gh",
        ['ङ'] = "ng",
        ['च'] = "ch",
        ['छ'] = "chh",
        ['ज'] = "j",
        ['झ'] = "jh",
        ['ञ'] = "ny",
        ['ट'] = "t",
        ['ठ'] = "th",
        ['ड'] = "d",
        ['ढ'] = "dh",
        ['ण'] = "n",
        ['त'] = "t",
        ['थ'] = "th",
        ['द'] = "d",
        ['ध'] = "dh",
        ['न'] = "n",
        ['प'] = "p",
        ['फ'] = "ph",
        ['ब'] = "b",
        ['भ'] = "bh",
        ['म'] = "m",
        ['य'] = "y",
        ['र'] = "r",
        ['ल'] = "l",
        ['व'] = "v",
        ['श'] = "sh",
        ['ष'] = "sh",
        ['स'] = "s",
        ['ह'] = "h",
        ['ळ'] = "l",
    };

    private static readonly Dictionary<char, string> MatraMap = new()
    {
        ['ा'] = "aa",
        ['ि'] = "i",
        ['ी'] = "ii",
        ['ु'] = "u",
        ['ू'] = "uu",
        ['े'] = "e",
        ['ै'] = "ai",
        ['ो'] = "o",
        ['ौ'] = "au",
        ['ृ'] = "ri",
    };

    private static readonly Dictionary<char, string> StandaloneVowelMap = new()
    {
        ['अ'] = "a",
        ['आ'] = "aa",
        ['इ'] = "i",
        ['ई'] = "ii",
        ['उ'] = "u",
        ['ऊ'] = "uu",
        ['ए'] = "e",
        ['ऐ'] = "ai",
        ['ओ'] = "o",
        ['औ'] = "au",
        ['ऋ'] = "ri",
    };

    private const char Halant = '\u094D'; // ्
    private const char Anusvara = '\u0902'; // ं
    private const char Chandrabindu = '\u0901'; // ँ

    // ── Public API ────────────────────────────────────────────────────────────

    // ── Nepali digit conversion ───────────────────────────────────────────────

    private static readonly char[] NepaliDigits = ['०', '१', '२', '३', '४', '५', '६', '७', '८', '९'];

    public static string ToNepaliDigits(int number)
    {
        var s = number.ToString();
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            chars[i] = char.IsDigit(s[i]) ? NepaliDigits[s[i] - '0'] : s[i];
        }

        return new string(chars);
    }

    /// <summary>
    /// Converts romanised Nepali text to Devanagari using a phonetic rule engine.
    /// Words are split on whitespace and '/' separators; each word is converted
    /// independently and the separators are preserved.
    /// </summary>
    public static string RomanToDevanagari(string roman)
    {
        if (string.IsNullOrEmpty(roman))
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder(roman.Length * 2);
        int i = 0;
        while (i < roman.Length)
        {
            char c = roman[i];
            // Pass through whitespace, digits, '/' separator, and punctuation
            if (char.IsWhiteSpace(c) || char.IsDigit(c) || c == '/' || char.IsPunctuation(c) || char.IsSymbol(c))
            {
                result.Append(c);
                i++;
                continue;
            }
            // Collect the current word token (letters only)
            int wordStart = i;
            while (i < roman.Length &&
                   !char.IsWhiteSpace(roman[i]) && !char.IsDigit(roman[i]) &&
                   roman[i] != '/' && !char.IsPunctuation(roman[i]) && !char.IsSymbol(roman[i]))
            {
                i++;
            }

            result.Append(ConvertWord(roman[wordStart..i]));
        }
        return result.ToString();
    }

    /// <summary>
    /// Converts Devanagari text to romanised Nepali.
    /// </summary>
    public static string DevanagariToRoman(string deva)
    {
        if (string.IsNullOrEmpty(deva))
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder(deva.Length * 2);
        int i = 0;
        while (i < deva.Length)
        {
            char c = deva[i];

            if (StandaloneVowelMap.TryGetValue(c, out var svr))
            {
                result.Append(svr);
                i++;
                continue;
            }

            if (ConsonantMap.TryGetValue(c, out var cr))
            {
                result.Append(cr);
                i++;
                // Look ahead: halant = no inherent vowel; matra = explicit vowel; otherwise = 'a'
                if (i < deva.Length && deva[i] == Halant)
                {
                    i++; // skip halant - next consonant follows immediately
                }
                else if (i < deva.Length && MatraMap.TryGetValue(deva[i], out var matraRoman))
                {
                    result.Append(matraRoman);
                    i++;
                }
                else
                {
                    result.Append('a'); // inherent vowel
                }
                continue;
            }

            if (MatraMap.TryGetValue(c, out var mr))
            {
                // Orphan matra (shouldn't happen in well-formed text; pass through)
                result.Append(mr);
                i++;
                continue;
            }

            if (c == Anusvara || c == Chandrabindu)
            {
                result.Append('m');
                i++;
                continue;
            }

            if (c == Halant)
            {
                i++; // skip stray halant
                continue;
            }

            // Pass through anything else (spaces, digits, punctuation)
            result.Append(c);
            i++;
        }
        return result.ToString();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string ConvertWord(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }

        var sb = new System.Text.StringBuilder(word.Length * 3);
        int i = 0;
        bool afterConsonant = false;

        while (i < word.Length)
        {
            bool matched = false;

            foreach (var p in Patterns)
            {
                if (!word.AsSpan(i).StartsWith(p.Roman, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (p.Consonant is not null)
                {
                    // Consonant or pre-built conjunct
                    if (afterConsonant)
                    {
                        sb.Append(Halant);
                    }

                    sb.Append(p.Consonant);
                    afterConsonant = true;
                }
                else
                {
                    // Vowel
                    if (afterConsonant)
                    {
                        // Matra - empty string for inherent 'a' means nothing is appended
                        sb.Append(p.Matra);
                    }
                    else
                    {
                        sb.Append(p.FullVowel);
                    }
                    afterConsonant = false;
                }

                i += p.Roman.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                // Unknown character - emit as-is and reset state
                if (afterConsonant)
                {
                    afterConsonant = false;
                }

                sb.Append(word[i]);
                i++;
            }
        }

        return sb.ToString();
    }
}
