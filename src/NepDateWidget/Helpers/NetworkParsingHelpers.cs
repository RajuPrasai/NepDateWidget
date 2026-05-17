namespace NepDateWidget.Helpers;

/// <summary>
/// Pure static parsing and inference logic for the Network Tools tab.
/// Extracted from NetworkToolsViewModel so it can be unit-tested independently.
/// No I/O. No side effects.
/// </summary>
internal static class NetworkParsingHelpers
{
    // ── OUI → Manufacturer (offline embedded table) ───────────────────────────

    internal static readonly Dictionary<string, string> OuiTable =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["00:00:0C"] = "Cisco",
            ["00:01:42"] = "Cisco",
            ["00:0A:41"] = "Cisco",
            ["00:50:56"] = "VMware",
            ["00:0C:29"] = "VMware",
            ["00:05:69"] = "VMware",
            ["00:1C:42"] = "Parallels",
            ["08:00:27"] = "VirtualBox / Oracle",
            ["52:54:00"] = "QEMU / KVM",
            ["00:50:F2"] = "Microsoft",
            ["28:18:78"] = "Microsoft",
            ["7C:1E:52"] = "Microsoft",
            ["00:1A:7D"] = "Dell",
            ["00:14:22"] = "Dell",
            ["18:03:73"] = "Dell",
            ["F0:1F:AF"] = "Dell",
            ["00:26:B9"] = "Dell",
            ["00:1E:C9"] = "HP",
            ["3C:D9:2B"] = "HP",
            ["FC:15:B4"] = "HP",
            ["00:23:AE"] = "HP",
            ["00:17:A4"] = "HP",
            ["00:1B:63"] = "Apple",
            ["34:36:3B"] = "Apple",
            ["A4:5E:60"] = "Apple",
            ["8C:85:90"] = "Apple",
            ["F0:D1:A9"] = "Apple",
            ["28:CF:E9"] = "Apple",
            ["00:25:00"] = "Apple",
            ["40:A6:D9"] = "Apple",
            ["00:16:3E"] = "Xen",
            ["00:1B:21"] = "Intel",
            ["8C:8D:28"] = "Intel",
            ["00:1F:3B"] = "Intel",
            ["00:23:14"] = "Intel",
            ["68:05:CA"] = "Intel",
            ["00:1C:C0"] = "Huawei",
            ["00:E0:FC"] = "Huawei",
            ["3C:F8:08"] = "Huawei",
            ["04:BD:70"] = "Huawei",
            ["70:F9:6D"] = "Huawei",
            ["00:17:F2"] = "Samsung",
            ["78:1F:DB"] = "Samsung",
            ["F4:7B:5E"] = "Samsung",
            ["D0:17:C2"] = "Samsung",
            ["A0:0B:BA"] = "Samsung",
            ["18:A6:F7"] = "Xiaomi",
            ["64:09:80"] = "Xiaomi",
            ["0C:1D:AF"] = "Xiaomi",
            ["50:64:2B"] = "Xiaomi",
            ["F4:8B:32"] = "Xiaomi",
            ["44:65:0D"] = "TP-Link",
            ["50:C7:BF"] = "TP-Link",
            ["8C:59:C3"] = "TP-Link",
            ["14:CF:92"] = "TP-Link",
            ["B0:4E:26"] = "TP-Link",
            ["00:18:E7"] = "Netgear",
            ["20:E5:2A"] = "Netgear",
            ["A0:40:A0"] = "Netgear",
            ["C4:3D:C7"] = "Netgear",
            ["9C:D3:6D"] = "Netgear",
            ["00:1A:2B"] = "ASUS",
            ["10:BF:48"] = "ASUS",
            ["50:46:5D"] = "ASUS",
            ["04:92:26"] = "ASUS",
            ["AC:9E:17"] = "ASUS",
            ["00:26:44"] = "D-Link",
            ["1C:7E:E5"] = "D-Link",
            ["C8:BE:19"] = "D-Link",
            ["F0:7D:68"] = "D-Link",
            ["28:10:7B"] = "D-Link",
            ["00:1A:11"] = "Google",
            ["94:EB:2C"] = "Google",
            ["F4:F5:D8"] = "Google",
            ["48:D6:D5"] = "Google",
            ["A4:77:33"] = "Google",
            ["B8:27:EB"] = "Raspberry Pi",
            ["DC:A6:32"] = "Raspberry Pi",
            ["E4:5F:01"] = "Raspberry Pi",
            ["00:11:32"] = "Synology",
            ["00:11:33"] = "Synology",
            ["00:11:34"] = "Synology",
            ["00:08:9B"] = "QNAP",
        };

    /// <summary>
    /// Returns the manufacturer name for a MAC address, or empty string if unknown.
    /// The MAC must be in colon-separated or hyphen-separated notation.
    /// </summary>
    internal static string LookupManufacturer(string mac)
    {
        if (mac is null || mac.Length < 8)
        {
            return string.Empty;
        }
        // Normalise hyphens to colons and upper-case
        var oui = mac[..8].Replace('-', ':').ToUpperInvariant();
        return OuiTable.TryGetValue(oui, out var mfr) ? mfr : string.Empty;
    }

    /// <summary>
    /// Infers a human-readable device type from hostname and manufacturer name.
    /// Purely keyword-based - not authoritative, but good enough for a scan summary.
    /// </summary>
    internal static string InferDeviceType(string hostName, string manufacturer)
    {
        var h = (hostName ?? string.Empty).ToLowerInvariant();
        var m = (manufacturer ?? string.Empty).ToLowerInvariant();

        if (m.Contains("raspberry pi"))
        {
            return "SBC";
        }

        if (m.Contains("vmware") || m.Contains("virtualbox") || m.Contains("qemu")
            || m.Contains("parallels") || m.Contains("xen"))
        {
            return "VM";
        }

        if (m.Contains("cisco") || h.Contains("router") || h.Contains("gateway") || h.Contains("gw-"))
        {
            return "Router/Switch";
        }

        if (m.Contains("tp-link") || m.Contains("netgear") || m.Contains("d-link")
            || m.Contains("asus") || m.Contains("huawei"))
        {
            return "Network";
        }

        if (m.Contains("apple"))
        {
            return "Apple device";
        }

        if (m.Contains("samsung") || m.Contains("xiaomi"))
        {
            return "Mobile/IoT";
        }

        if (h.Contains("printer") || h.Contains("print"))
        {
            return "Printer";
        }

        if (h.Contains("phone") || h.Contains("iphone") || h.Contains("android"))
        {
            return "Phone";
        }

        if (h.Contains("nas") || m.Contains("synology") || m.Contains("qnap"))
        {
            return "NAS";
        }

        if (h.Contains("camera") || h.Contains("cam"))
        {
            return "Camera";
        }

        return "Host";
    }

    /// <summary>
    /// Extracts key registration fields from a raw WHOIS response.
    /// Returns a compact summary with the full raw text appended.
    /// If no recognised fields are found, returns the raw text unchanged.
    /// </summary>
    internal static string FormatWhoisOutput(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var interestingKeys = new[]
        {
            "Domain Name", "Registrar", "Registrar URL", "Updated Date",
            "Creation Date", "Registry Expiry Date", "Registrar Registration Expiration Date",
            "Name Server", "DNSSEC", "Registrant Organization", "Registrant Country",
            "Registrant Email", "Tech Email"
        };

        var summary = new System.Text.StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 1)
            {
                continue;
            }

            var key = trimmed[..colonIdx].Trim();
            if (!interestingKeys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!seen.Add(key))
            {
                continue;
            }

            var val = trimmed[(colonIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(val))
            {
                summary.AppendLine($"{key}: {val}");
            }
        }

        if (summary.Length == 0)
        {
            return raw.Trim();
        }

        summary.AppendLine();
        summary.AppendLine("--- Full response ---");
        summary.Append(raw.Trim());
        return summary.ToString().Trim();
    }
}
