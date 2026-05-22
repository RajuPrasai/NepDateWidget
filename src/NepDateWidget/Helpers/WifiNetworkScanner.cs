using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace NepDateWidget.Helpers;

/// <summary>
/// Enumerates saved WiFi profiles via wlanapi.dll P/Invoke.
/// Returns each profile's SSID and the QR-standard authentication type string.
/// Password retrieval is deliberately omitted: WlanGetProfile with
/// WLAN_PROFILE_GET_PLAINTEXT_KEY requires elevation on profiles not owned by
/// the current session - there is no safe way to auto-fill the password.
/// </summary>
internal static class WifiNetworkScanner
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved,
        out uint negotiatedVersion, out IntPtr clientHandle);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved,
        out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetProfileList(IntPtr clientHandle,
        ref Guid interfaceGuid, IntPtr reserved, out IntPtr ppProfileList);

    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanGetProfile(IntPtr clientHandle,
        ref Guid interfaceGuid,
        [MarshalAs(UnmanagedType.LPWStr)] string profileName,
        IntPtr reserved,
        out IntPtr ppstrProfileXml,
        ref uint pdwFlags,
        out uint pdwGrantedAccess);

    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr clientHandle,
        ref Guid interfaceGuid, uint opCode, IntPtr reserved,
        out uint pdwDataSize, out IntPtr ppData, out uint pWlanOpcodeValueType);

    // ── Constants ─────────────────────────────────────────────────────────────

    private const uint WLAN_API_VERSION_2_0 = 0x00000002;
    private const uint ERROR_SUCCESS = 0;
    private const uint WLAN_INTF_OPCODE_CURRENT_CONNECTION = 7;

    // WLAN_INTERFACE_INFO layout:
    //   GUID  InterfaceGuid       - 16 bytes
    //   WCHAR[256] Description    - 512 bytes
    //   DWORD isState             - 4 bytes
    //   Total = 532 bytes
    private const int WLAN_INTERFACE_INFO_SIZE = 532;

    // WLAN_PROFILE_INFO layout:
    //   WCHAR[256] strProfileName - 512 bytes
    //   DWORD dwFlags             - 4 bytes
    //   Total = 516 bytes
    private const int WLAN_PROFILE_INFO_SIZE = 516;

    // List headers: DWORD dwNumberOfItems + DWORD dwIndex = 8 bytes before the array
    private const int LIST_HEADER_SIZE = 8;

    // WLAN_CONNECTION_ATTRIBUTES layout (offsets relevant here):
    //   DWORD isState             - offset 0
    //   DWORD wlanConnectionMode  - offset 4
    //   WCHAR[256] strProfileName - offset 8
    private const int CONN_ATTRS_PROFILE_NAME_OFFSET = 8;

    // Instructs WlanGetProfile to return the plaintext key in keyMaterial.
    // Succeeds without elevation for profiles created by the current user on
    // Windows 8+. For all-user / admin-managed profiles the flag is accepted
    // but the returned keyMaterial is empty - graceful fallback.
    private const uint WLAN_PROFILE_GET_PLAINTEXT_KEY = 0x00000004;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all saved WiFi profiles and the SSID of the currently connected network.
    /// Returns empty lists on any error (no WiFi adapter, service unavailable, etc.).
    /// </summary>
    public static (List<WifiProfile> Profiles, string ConnectedSsid) Scan()
    {
        try
        {
            if (WlanOpenHandle(WLAN_API_VERSION_2_0, IntPtr.Zero, out _, out IntPtr hClient) != ERROR_SUCCESS)
                return ([], string.Empty);

            try
            {
                return ScanWithHandle(hClient);
            }
            finally
            {
                WlanCloseHandle(hClient, IntPtr.Zero);
            }
        }
        catch
        {
            return ([], string.Empty);
        }
    }

    // ── Private implementation ────────────────────────────────────────────────

    private static (List<WifiProfile> Profiles, string ConnectedSsid) ScanWithHandle(IntPtr hClient)
    {
        if (WlanEnumInterfaces(hClient, IntPtr.Zero, out IntPtr pInterfaceList) != ERROR_SUCCESS)
            return ([], string.Empty);

        try
        {
            uint count = (uint)Marshal.ReadInt32(pInterfaceList, 0);
            if (count == 0)
                return ([], string.Empty);

            // Read the GUID of the first interface: 16 bytes at offset LIST_HEADER_SIZE
            byte[] guidBytes = new byte[16];
            Marshal.Copy(IntPtr.Add(pInterfaceList, LIST_HEADER_SIZE), guidBytes, 0, 16);
            Guid interfaceGuid = new(guidBytes);

            string connectedSsid = GetConnectedProfileName(hClient, interfaceGuid);
            List<WifiProfile> profiles = GetProfiles(hClient, interfaceGuid);

            return (profiles, connectedSsid);
        }
        finally
        {
            WlanFreeMemory(pInterfaceList);
        }
    }

    private static List<WifiProfile> GetProfiles(IntPtr hClient, Guid interfaceGuid)
    {
        if (WlanGetProfileList(hClient, ref interfaceGuid, IntPtr.Zero, out IntPtr pProfileList) != ERROR_SUCCESS)
            return [];

        try
        {
            uint count = (uint)Marshal.ReadInt32(pProfileList, 0);
            var profiles = new List<WifiProfile>((int)count);
            IntPtr firstEntry = IntPtr.Add(pProfileList, LIST_HEADER_SIZE);

            for (uint i = 0; i < count; i++)
            {
                IntPtr entry = IntPtr.Add(firstEntry, (int)(i * WLAN_PROFILE_INFO_SIZE));
                string profileName = Marshal.PtrToStringUni(entry, 256)?.TrimEnd('\0') ?? string.Empty;

                if (string.IsNullOrEmpty(profileName))
                    continue;

                (string ssid, string qrAuthType, string password) = GetProfileInfo(hClient, interfaceGuid, profileName);
                profiles.Add(new WifiProfile(ssid, qrAuthType, password));
            }

            return profiles;
        }
        finally
        {
            WlanFreeMemory(pProfileList);
        }
    }

    private static string GetConnectedProfileName(IntPtr hClient, Guid interfaceGuid)
    {
        if (WlanQueryInterface(hClient, ref interfaceGuid,
                WLAN_INTF_OPCODE_CURRENT_CONNECTION, IntPtr.Zero,
                out _, out IntPtr ppData, out _) != ERROR_SUCCESS)
            return string.Empty;

        try
        {
            if (ppData == IntPtr.Zero)
                return string.Empty;

            IntPtr namePtr = IntPtr.Add(ppData, CONN_ATTRS_PROFILE_NAME_OFFSET);
            return Marshal.PtrToStringUni(namePtr, 256)?.TrimEnd('\0') ?? string.Empty;
        }
        finally
        {
            WlanFreeMemory(ppData);
        }
    }

    private static (string Ssid, string QrAuthType, string Password) GetProfileInfo(IntPtr hClient, Guid interfaceGuid, string profileName)
    {
        // Pass WLAN_PROFILE_GET_PLAINTEXT_KEY so Windows returns the plaintext key
        // in keyMaterial when the caller has the required permission.  For profiles
        // the current user does not own the element will simply be empty - no error.
        uint flags = WLAN_PROFILE_GET_PLAINTEXT_KEY;
        if (WlanGetProfile(hClient, ref interfaceGuid, profileName,
                IntPtr.Zero, out IntPtr xmlPtr, ref flags, out _) != ERROR_SUCCESS
            || xmlPtr == IntPtr.Zero)
            return (profileName, "WPA", string.Empty);

        try
        {
            string xml = Marshal.PtrToStringUni(xmlPtr) ?? string.Empty;
            string ssid     = ParseSsid(xml) ?? profileName;
            string authType = ParseQrAuthType(xml);
            string password = ParsePassword(xml);
            return (ssid, authType, password);
        }
        finally
        {
            WlanFreeMemory(xmlPtr);
        }
    }

    private static string? ParseSsid(string profileXml)
    {
        try
        {
            var doc = XDocument.Parse(profileXml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            return doc.Descendants(ns + "SSIDConfig")
                      .Descendants(ns + "SSID")
                      .Descendants(ns + "name")
                      .FirstOrDefault()?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string ParseQrAuthType(string profileXml)
    {
        try
        {
            var doc = XDocument.Parse(profileXml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            string? auth = doc.Descendants(ns + "authentication").FirstOrDefault()?.Value?.Trim();
            return auth?.ToUpperInvariant() switch
            {
                "OPEN" => "nopass",
                "OWE"  => "nopass",
                "WEP"  => "WEP",
                _      => "WPA"
            };
        }
        catch
        {
            return "WPA";
        }
    }

    // Returns the plaintext password from keyMaterial only when the element is
    // marked protected=false (meaning WLAN_PROFILE_GET_PLAINTEXT_KEY worked).
    // Returns empty string for open networks and when the flag had no effect.
    private static string ParsePassword(string profileXml)
    {
        try
        {
            var doc = XDocument.Parse(profileXml);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var sharedKey = doc.Descendants(ns + "sharedKey").FirstOrDefault();
            if (sharedKey is null) return string.Empty;

            string? prot = sharedKey.Element(ns + "protected")?.Value?.Trim();
            if (!string.Equals(prot, "false", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return sharedKey.Element(ns + "keyMaterial")?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>SSID, QR auth type, and optional plaintext password for a saved WiFi profile.</summary>
/// <param name="Ssid">The network name as it appears in the QR WIFI string S: field.</param>
/// <param name="QrAuthType">One of "WPA", "WEP", or "nopass" per the WiFi QR spec.</param>
/// <param name="Password">Plaintext key if retrievable without elevation; empty otherwise.</param>
public sealed record WifiProfile(string Ssid, string QrAuthType, string Password);
