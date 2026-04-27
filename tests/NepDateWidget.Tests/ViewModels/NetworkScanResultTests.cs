using NepDateWidget.Models;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for NetworkScanResult model integrity.
/// </summary>
public class NetworkScanResultTests
{
    [Fact]
    public void DefaultConstruction_AllStringsEmpty_RttZero()
    {
        var r = new NetworkScanResult();
        Assert.Equal(string.Empty, r.IpAddress);
        Assert.Equal(string.Empty, r.HostName);
        Assert.Equal(string.Empty, r.MacAddress);
        Assert.Equal(string.Empty, r.Manufacturer);
        Assert.Equal(string.Empty, r.DeviceType);
        Assert.Equal(string.Empty, r.Status);
        Assert.Equal(0L, r.RoundTripMs);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var r = new NetworkScanResult
        {
            IpAddress    = "192.168.1.42",
            HostName     = "desktop-01",
            MacAddress   = "AA:BB:CC:DD:EE:FF",
            Manufacturer = "Acme Corp",
            DeviceType   = "Host",
            Status       = "Online",
            RoundTripMs  = 3
        };

        Assert.Equal("192.168.1.42",        r.IpAddress);
        Assert.Equal("desktop-01",           r.HostName);
        Assert.Equal("AA:BB:CC:DD:EE:FF",   r.MacAddress);
        Assert.Equal("Acme Corp",            r.Manufacturer);
        Assert.Equal("Host",                 r.DeviceType);
        Assert.Equal("Online",               r.Status);
        Assert.Equal(3L,                     r.RoundTripMs);
    }
}
