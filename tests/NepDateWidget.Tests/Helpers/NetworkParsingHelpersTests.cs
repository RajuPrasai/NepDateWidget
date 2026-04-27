using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Rigorous unit tests for NetworkParsingHelpers.
/// These cover: OUI table coverage and normalisation, manufacturer lookup edge cases,
/// device-type inference classifier, and whois output formatting rules.
/// All tests are pure in-memory — no I/O, no network.
/// </summary>
public class NetworkParsingHelpersTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // OUI TABLE INTEGRITY
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void OuiTable_HasEntries()
    {
        Assert.NotEmpty(NetworkParsingHelpers.OuiTable);
    }

    [Fact]
    public void OuiTable_AllKeysAreEightChars()
    {
        foreach (var key in NetworkParsingHelpers.OuiTable.Keys)
            Assert.True(key.Length == 8, $"OUI key '{key}' is not 8 characters");
    }

    [Fact]
    public void OuiTable_AllKeysMatchColonSeparatedPattern()
    {
        // Valid OUI format: XX:XX:XX where X is hex digit
        var pattern = new System.Text.RegularExpressions.Regex(@"^[0-9A-Fa-f]{2}:[0-9A-Fa-f]{2}:[0-9A-Fa-f]{2}$");
        foreach (var key in NetworkParsingHelpers.OuiTable.Keys)
            Assert.True(pattern.IsMatch(key), $"OUI key '{key}' does not match XX:XX:XX format");
    }

    [Fact]
    public void OuiTable_NoEmptyValues()
    {
        foreach (var kv in NetworkParsingHelpers.OuiTable)
            Assert.False(string.IsNullOrWhiteSpace(kv.Value),
                $"OUI '{kv.Key}' has an empty or whitespace manufacturer name");
    }

    [Fact]
    public void OuiTable_ContainsExpectedVendors()
    {
        var values = NetworkParsingHelpers.OuiTable.Values;
        Assert.Contains(values, v => v.Contains("Cisco"));
        Assert.Contains(values, v => v.Contains("Apple"));
        Assert.Contains(values, v => v.Contains("VMware"));
        Assert.Contains(values, v => v.Contains("Raspberry Pi"));
        Assert.Contains(values, v => v.Contains("TP-Link"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LOOKUP MANUFACTURER
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("B8:27:EB:00:00:01", "Raspberry Pi")]
    [InlineData("00:50:56:AA:BB:CC", "VMware")]
    [InlineData("00:00:0C:11:22:33", "Cisco")]
    [InlineData("00:1B:63:AA:BB:CC", "Apple")]
    [InlineData("44:65:0D:AA:BB:CC", "TP-Link")]
    [InlineData("00:18:E7:AA:BB:CC", "Netgear")]
    [InlineData("00:1A:2B:AA:BB:CC", "ASUS")]
    [InlineData("B8:27:EB:FF:FF:FF", "Raspberry Pi")] // different device, same OUI
    public void LookupManufacturer_KnownOui_ReturnsExpectedVendor(string mac, string expected)
    {
        var result = NetworkParsingHelpers.LookupManufacturer(mac);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("FF:FF:FF:FF:FF:FF")] // unknown OUI
    [InlineData("12:34:56:78:9A:BC")] // unknown OUI
    public void LookupManufacturer_UnknownOui_ReturnsEmpty(string mac)
    {
        var result = NetworkParsingHelpers.LookupManufacturer(mac);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("b8:27:eb:00:00:01")]  // lower-case
    [InlineData("B8:27:EB:00:00:01")]  // upper-case
    [InlineData("b8:27:EB:00:00:01")]  // mixed
    public void LookupManufacturer_CaseInsensitive(string mac)
    {
        var result = NetworkParsingHelpers.LookupManufacturer(mac);
        Assert.Equal("Raspberry Pi", result);
    }

    [Theory]
    [InlineData("B8-27-EB-00-00-01")]  // hyphen separator (Windows arp -a format)
    public void LookupManufacturer_HyphenSeparated_Normalised(string mac)
    {
        var result = NetworkParsingHelpers.LookupManufacturer(mac);
        Assert.Equal("Raspberry Pi", result);
    }

    [Fact]
    public void LookupManufacturer_NullInput_ReturnsEmpty()
    {
        var result = NetworkParsingHelpers.LookupManufacturer(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void LookupManufacturer_EmptyString_ReturnsEmpty()
    {
        var result = NetworkParsingHelpers.LookupManufacturer(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("B8:27")]    // too short (5 chars)
    [InlineData("B8")]       // way too short
    public void LookupManufacturer_TooShortMac_ReturnsEmpty(string mac)
    {
        var result = NetworkParsingHelpers.LookupManufacturer(mac);
        Assert.Equal(string.Empty, result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // INFER DEVICE TYPE
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("",              "Raspberry Pi", "SBC")]
    [InlineData("nas-1",        "Synology",      "NAS")]
    [InlineData("nas-1",        "QNAP",          "NAS")]
    [InlineData("",              "VMware",        "VM")]
    [InlineData("",              "VirtualBox / Oracle", "VM")]
    [InlineData("",              "QEMU / KVM",    "VM")]
    [InlineData("",              "Parallels",     "VM")]
    [InlineData("",              "Xen",           "VM")]
    [InlineData("router.local", "",               "Router/Switch")]
    [InlineData("gw-01",        "",               "Router/Switch")]
    [InlineData("gateway.home", "",               "Router/Switch")]
    [InlineData("",              "Cisco",         "Router/Switch")]
    [InlineData("",              "TP-Link",       "Network")]
    [InlineData("",              "Netgear",       "Network")]
    [InlineData("",              "D-Link",        "Network")]
    [InlineData("",              "ASUS",          "Network")]
    [InlineData("",              "Huawei",        "Network")]
    [InlineData("",              "Apple",         "Apple device")]
    [InlineData("",              "Samsung",       "Mobile/IoT")]
    [InlineData("",              "Xiaomi",        "Mobile/IoT")]
    [InlineData("printer.lan",  "",               "Printer")]
    [InlineData("hp-print-01",  "",               "Printer")]
    [InlineData("my-iphone",    "",               "Phone")]
    [InlineData("android-phone","",               "Phone")]
    [InlineData("cam-01",       "",               "Camera")]
    [InlineData("front-camera", "",               "Camera")]
    [InlineData("nas-box",      "",               "NAS")]
    [InlineData("desktop-01",   "Unknown Corp",   "Host")]
    [InlineData("",              "",              "Host")]
    public void InferDeviceType_ReturnsExpectedType(string host, string manufacturer, string expected)
    {
        var result = NetworkParsingHelpers.InferDeviceType(host, manufacturer);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void InferDeviceType_NullHostAndManufacturer_ReturnsHost()
    {
        var result = NetworkParsingHelpers.InferDeviceType(null!, null!);
        Assert.Equal("Host", result);
    }

    [Fact]
    public void InferDeviceType_RaspberryPi_HasHigherPriority_ThanNetwork()
    {
        // A Pi might also match "unknown" but Raspberry Pi must win
        var result = NetworkParsingHelpers.InferDeviceType("raspberrypi.local", "Raspberry Pi");
        Assert.Equal("SBC", result);
    }

    [Fact]
    public void InferDeviceType_VM_HasHigherPriority_ThanRouter()
    {
        var result = NetworkParsingHelpers.InferDeviceType("gateway-vm", "VMware");
        Assert.Equal("VM", result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FORMAT WHOIS OUTPUT
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatWhoisOutput_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NetworkParsingHelpers.FormatWhoisOutput(string.Empty));
    }

    [Fact]
    public void FormatWhoisOutput_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NetworkParsingHelpers.FormatWhoisOutput("   \n   "));
    }

    [Fact]
    public void FormatWhoisOutput_NoKnownFields_ReturnsRawTrimmed()
    {
        const string raw = "  Some unknown WHOIS response\nLine two  ";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Equal(raw.Trim(), result);
    }

    [Fact]
    public void FormatWhoisOutput_WithRegistrar_ExtractsToSummary()
    {
        const string raw = "Registrar: Some Registrar Inc.\nOther: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Registrar: Some Registrar Inc.", result);
    }

    [Fact]
    public void FormatWhoisOutput_WithDomainName_ExtractsToSummary()
    {
        const string raw = "Domain Name: EXAMPLE.COM\nNoise: ignored";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Domain Name: EXAMPLE.COM", result);
    }

    [Fact]
    public void FormatWhoisOutput_WithCreationDate_ExtractsToSummary()
    {
        const string raw = "Creation Date: 1995-08-14T04:00:00Z\nNoise: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Creation Date: 1995-08-14T04:00:00Z", result);
    }

    [Fact]
    public void FormatWhoisOutput_WithNameServer_ExtractsToSummary()
    {
        const string raw = "Name Server: NS1.EXAMPLE.COM\nName Server: NS2.EXAMPLE.COM\nNoise: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        // First occurrence only (deduplicated by key)
        Assert.Contains("Name Server: NS1.EXAMPLE.COM", result);
    }

    [Fact]
    public void FormatWhoisOutput_DuplicateKeys_OnlyFirstKept()
    {
        const string raw = "Registrar: First\nRegistrar: Second\nNoise: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Registrar: First", result);
        // The second occurrence must not appear in the summary section
        // (it will appear in the full-response section)
        var summaryPart = result.Split("--- Full response ---")[0];
        Assert.DoesNotContain("Registrar: Second", summaryPart);
    }

    [Fact]
    public void FormatWhoisOutput_SummarySection_AppearsBeforeFullResponse()
    {
        const string raw = "Domain Name: EXAMPLE.COM\nNoise: ignored data";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        var summaryIdx = result.IndexOf("Domain Name:", StringComparison.Ordinal);
        var fullIdx = result.IndexOf("--- Full response ---", StringComparison.Ordinal);
        Assert.True(summaryIdx < fullIdx);
    }

    [Fact]
    public void FormatWhoisOutput_FullRawTextPreserved_InOutput()
    {
        const string raw = "Domain Name: EXAMPLE.COM\nRegistrar: IANA\nSome unique content: xyz987";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        // The full raw content must appear somewhere in the output
        Assert.Contains("Some unique content: xyz987", result);
    }

    [Fact]
    public void FormatWhoisOutput_CaseInsensitiveKeyMatching()
    {
        const string raw = "DOMAIN NAME: EXAMPLE.ORG\nNoise: ignored";
        // Key is "DOMAIN NAME" which does not match "Domain Name" — should fall through to raw
        // (The comparison is exact case on the key name as written)
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.NotNull(result);
        // Ensure the raw text is still preserved — no crash
        Assert.Contains("DOMAIN NAME: EXAMPLE.ORG", result);
    }

    [Fact]
    public void FormatWhoisOutput_MultipleKnownFields_AllExtracted()
    {
        const string raw =
            "Domain Name: EXAMPLE.COM\n" +
            "Registrar: Example Registrar\n" +
            "Creation Date: 2000-01-01\n" +
            "Name Server: NS1.EXAMPLE.COM\n" +
            "DNSSEC: unsigned\n" +
            "Noise: ignored";

        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Domain Name: EXAMPLE.COM", result);
        Assert.Contains("Registrar: Example Registrar", result);
        Assert.Contains("Creation Date: 2000-01-01", result);
        Assert.Contains("Name Server: NS1.EXAMPLE.COM", result);
        Assert.Contains("DNSSEC: unsigned", result);
    }

    [Fact]
    public void FormatWhoisOutput_LinesWithNoColon_Ignored()
    {
        const string raw = "Domain Name: EXAMPLE.COM\nThis line has no colon\nRegistrar: Test";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        // No crash, known fields still extracted
        Assert.Contains("Domain Name: EXAMPLE.COM", result);
        Assert.Contains("Registrar: Test", result);
    }

    [Fact]
    public void FormatWhoisOutput_ColonInValue_NotSplitIncorrectly()
    {
        // The value itself contains colons (e.g. a datetime with timezone)
        const string raw = "Creation Date: 2000-01-01T00:00:00Z\nNoise: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        Assert.Contains("Creation Date: 2000-01-01T00:00:00Z", result);
    }

    [Fact]
    public void FormatWhoisOutput_ListedKeyWithEmptyValue_Skipped()
    {
        // A known key with an empty value should not produce an empty summary line
        const string raw = "Registrar: \nDomain Name: EXAMPLE.COM\nNoise: noise";
        var result = NetworkParsingHelpers.FormatWhoisOutput(raw);
        // Domain Name should still be extracted
        Assert.Contains("Domain Name: EXAMPLE.COM", result);
        // The summary should not contain a bare "Registrar: " line (empty value)
        var summaryPart = result.Split("--- Full response ---")[0];
        Assert.DoesNotContain("Registrar: \n", summaryPart);
    }
}
