namespace NepDateWidget.Models;

/// <summary>
/// Represents a single host discovered during an Advanced IP Scan.
/// All properties are mutable so WPF binding can update the row
/// as ARP / hostname resolution completes asynchronously.
/// </summary>
public sealed class NetworkScanResult
{
    public string IpAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long RoundTripMs { get; set; }
    public bool IsLocalDevice { get; set; }
    public bool IsGateway { get; set; }
}
