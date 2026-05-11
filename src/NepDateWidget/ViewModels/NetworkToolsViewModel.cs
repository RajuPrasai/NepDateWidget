using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// ViewModel for the Network Tools tab.
/// Six sub-modes: My IP (0), Ping (1), IP Scanner (2), Traceroute (3), Whois (4), DNS (5).
/// All network calls run on a background thread; results are marshalled back via
/// System.Windows.Application.Current.Dispatcher to avoid cross-thread binding errors.
/// No new dependencies are introduced - HttpClient, Ping, Process, TcpClient, and
/// System.Net.Dns are all in the BCL.
/// </summary>
public sealed class NetworkToolsViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;

    // ── HttpClient singleton (thread-safe, reusable) ──────────────────────────
    // One instance for the lifetime of the VM; disposed when the tab/app closes.
    // SocketsHttpHandler with PooledConnectionLifetime prevents stale DNS entries
    // from persisting indefinitely (the default static HttpClient never refreshes DNS).
    private static readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // ═════════════════════════════════════════════════════════════════════════
    // MODE
    // ═════════════════════════════════════════════════════════════════════════

    private int _activeMode;
    public int ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                Log.Action($"network mode → {value}");
                OnPropertyChanged(nameof(IsModeMyIp));
                OnPropertyChanged(nameof(IsModePing));
                OnPropertyChanged(nameof(IsModeScan));
                OnPropertyChanged(nameof(IsModeTrace));
                OnPropertyChanged(nameof(IsModeWhois));
                OnPropertyChanged(nameof(IsModeDns));
            }
        }
    }

    public bool IsModeMyIp   { get => _activeMode == 0; set { if (value) ActiveMode = 0; } }
    public bool IsModePing   { get => _activeMode == 1; set { if (value) ActiveMode = 1; } }
    public bool IsModeScan   { get => _activeMode == 2; set { if (value) ActiveMode = 2; } }
    public bool IsModeTrace  { get => _activeMode == 3; set { if (value) ActiveMode = 3; } }
    public bool IsModeWhois  { get => _activeMode == 4; set { if (value) ActiveMode = 4; } }
    public bool IsModeDns    { get => _activeMode == 5; set { if (value) ActiveMode = 5; } }

    // ═════════════════════════════════════════════════════════════════════════
    // SHARED BUSY / CANCELLATION
    // ═════════════════════════════════════════════════════════════════════════

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }
    public bool IsNotBusy => !_isBusy;

    private CancellationTokenSource? _cts;

    private void CancelRunning()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private CancellationToken BeginOperation()
    {
        CancelRunning();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        return _cts.Token;
    }

    private void EndOperation()
    {
        IsBusy = false;
        // Force WPF to re-evaluate all CanExecute predicates immediately so buttons
        // re-enable as soon as the operation completes, not on the next user input.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(System.Windows.Input.CommandManager.InvalidateRequerySuggested));
    }

    /// <summary>
    /// Validates that a string is a safe hostname or IP address before passing
    /// it to external processes (tracert, nbtstat, arp). Rejects shell metacharacters.
    /// </summary>
    private static bool IsValidHostOrIp(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 253)
            return false;
        // Allow only alphanumeric, dots, hyphens, colons (IPv6), and square brackets
        foreach (var c in input)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != ':' && c != '[' && c != ']')
                return false;
        }
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 0: MY IP
    // ═════════════════════════════════════════════════════════════════════════

    private string _myIpResult = string.Empty;
    public string MyIpResult
    {
        get => _myIpResult;
        private set => SetProperty(ref _myIpResult, value);
    }

    // ── Mode-switch commands ───────────────────────────────────────────────────
    public ICommand SetModeMyIpCommand  { get; }
    public ICommand SetModePingCommand  { get; }
    public ICommand SetModeScanCommand  { get; }
    public ICommand SetModeTraceCommand { get; }
    public ICommand SetModeWhoisCommand { get; }
    public ICommand SetModeDnsCommand   { get; }

    public ICommand FetchMyIpCommand { get; }
    public ICommand CopyMyIpCommand { get; }

    private async Task FetchMyIpAsync()
    {
        var ct = BeginOperation();
        MyIpResult = _loc.Get("net.loading");
        try
        {
            var lines = new List<string>();

            // IPv4 - ipify returns a plain IP string
            try
            {
                var ipv4 = await _http.GetStringAsync("https://api.ipify.org", ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ipv4))
                    lines.Add($"IPv4 (Public):  {ipv4.Trim()}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // offline or cancel - handled below
            }

            // IPv6 - api64.ipify returns IPv6 when available, otherwise IPv4
            try
            {
                var ipv6 = await _http.GetStringAsync("https://api64.ipify.org", ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ipv6) && ipv6.Trim().Contains(':'))
                    lines.Add($"IPv6 (Public):  {ipv6.Trim()}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // offline or cancel - handled below
            }

            // Local IPs from network interfaces (always available offline)
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    var s = addr.Address.ToString();
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        lines.Add($"Local IPv4:     {s}  ({iface.Name})");
                    else if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                             && !addr.Address.IsIPv6LinkLocal)
                        lines.Add($"Local IPv6:     {s}  ({iface.Name})");
                }
            }

            if (ct.IsCancellationRequested) return;

            Dispatch(() =>
            {
                MyIpResult = lines.Count > 0
                    ? string.Join(Environment.NewLine, lines)
                    : _loc.Get("net.offline");
            });
        }
        catch (Exception ex)
        {
            Log.Error("FetchMyIp failed", ex);
            Dispatch(() => MyIpResult = _loc.Get("net.error"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 1: PING
    // ═════════════════════════════════════════════════════════════════════════

    private string _pingHost = string.Empty;
    public string PingHost
    {
        get => _pingHost;
        set => SetProperty(ref _pingHost, value);
    }

    private int _pingCount = 4;
    public int PingCount
    {
        get => _pingCount;
        set => SetProperty(ref _pingCount, Math.Clamp(value, 1, 10));
    }

    private string _pingResult = string.Empty;
    public string PingResult
    {
        get => _pingResult;
        private set => SetProperty(ref _pingResult, value);
    }

    public ICommand PingCommand { get; }
    public ICommand CopyPingCommand { get; }

    private async Task RunPingAsync()
    {
        var host = _pingHost.Trim();
        if (string.IsNullOrEmpty(host)) return;
        if (!IsValidHostOrIp(host)) { PingResult = _loc.Get("net.error"); return; }

        var ct = BeginOperation();
        PingResult = _loc.Get("net.loading");
        try
        {
            var results = new List<string> { $"Ping {host} - {_pingCount} packets:" };
            int sent = 0, received = 0;
            long minMs = long.MaxValue, maxMs = 0, totalMs = 0;

            using var ping = new Ping();
            for (int i = 0; i < _pingCount; i++)
            {
                if (ct.IsCancellationRequested) break;
                sent++;
                try
                {
                    var reply = await ping.SendPingAsync(host, 3000).ConfigureAwait(false);
                    if (reply.Status == IPStatus.Success)
                    {
                        received++;
                        minMs = Math.Min(minMs, reply.RoundtripTime);
                        maxMs = Math.Max(maxMs, reply.RoundtripTime);
                        totalMs += reply.RoundtripTime;
                        results.Add($"  [{i + 1}] {reply.Address} - {reply.RoundtripTime} ms  TTL={reply.Options?.Ttl}");
                    }
                    else
                    {
                        results.Add($"  [{i + 1}] {reply.Status}");
                    }
                }
                catch (PingException ex)
                {
                    results.Add($"  [{i + 1}] Error: {ex.InnerException?.Message ?? ex.Message}");
                }
                await Task.Delay(200, ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested) return;

            double loss = sent == 0 ? 0 : (sent - received) * 100.0 / sent;
            results.Add(string.Empty);
            results.Add($"Packets: Sent={sent}, Received={received}, Lost={sent - received} ({loss:F0}% loss)");
            if (received > 0)
            {
                double avg = (double)totalMs / received;
                results.Add($"RTT: Min={minMs} ms, Max={maxMs} ms, Avg={avg:F1} ms");
            }

            Dispatch(() => PingResult = string.Join(Environment.NewLine, results));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error("Ping failed", ex);
            Dispatch(() => PingResult = _loc.Get("net.error"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 2: ADVANCED IP SCANNER
    // ═════════════════════════════════════════════════════════════════════════

    public ObservableCollection<NetworkScanResult> ScanResults { get; } = new();

    private string _scanStatus = string.Empty;
    public string ScanStatus
    {
        get => _scanStatus;
        private set => SetProperty(ref _scanStatus, value);
    }

    public ICommand ScanCommand { get; }

    private async Task RunScanAsync()
    {
        var ct = BeginOperation();
        Dispatch(() =>
        {
            ScanResults.Clear();
            ScanStatus = _loc.Get("net.scanning");
        });

        try
        {
            // Determine the local subnet from the first active interface with a gateway
            var (localIp, subnetMask, gatewayIp) = GetLocalSubnet();
            if (localIp == null)
            {
                Dispatch(() => ScanStatus = _loc.Get("net.no_network"));
                return;
            }

            var localIpStr = localIp.ToString();
            var gatewayIpStr = gatewayIp?.ToString() ?? string.Empty;

            var ipBytes = localIp.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            var netBytes = ipBytes.Zip(maskBytes, (a, m) => (byte)(a & m)).ToArray();

            // Count host addresses - limit to /24 max to avoid 65535-host scans on larger subnets
            uint maskBits = 0;
            foreach (var b in maskBytes)
                for (int i = 7; i >= 0; i--)
                    if ((b >> i & 1) == 1) maskBits++;
            if (maskBits < 24) maskBits = 24;   // treat /24 as minimum grain

            int hostCount = (int)Math.Pow(2, 32 - maskBits) - 2;   // exclude net + broadcast

            var tasks = new List<Task>();
            int scanned = 0;
            int found = 0;

            // Parallelise - 64 concurrent pings at most
            const int MaxConcurrentScans = 64;
            using var sem = new SemaphoreSlim(MaxConcurrentScans);

            for (int i = 1; i <= hostCount && !ct.IsCancellationRequested; i++)
            {
                int host = i;   // capture
                await sem.WaitAsync(ct).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var addr = BuildIp(netBytes, host);
                        using var ping = new Ping();
                        var reply = await ping.SendPingAsync(addr, 800).ConfigureAwait(false);
                        Interlocked.Increment(ref scanned);

                        if (reply.Status == IPStatus.Success)
                        {
                            var ip = reply.Address.ToString();
                            var isLocal = ip == localIpStr;
                            var isGateway = ip == gatewayIpStr;
                            var mac = GetMacFromArp(ip);
                            var mfr = mac.Length >= 8 ? LookupManufacturer(mac) : string.Empty;

                            // DNS hostname
                            string hostName;
                            try { hostName = (await Dns.GetHostEntryAsync(ip).ConfigureAwait(false)).HostName; }
                            catch { hostName = string.Empty; }

                            // If DNS returned the IP itself or nothing, try NetBIOS
                            if (string.IsNullOrEmpty(hostName) || hostName == ip)
                            {
                                var nbName = GetNetBiosName(ip);
                                if (!string.IsNullOrEmpty(nbName))
                                    hostName = nbName;
                            }

                            // For local device, use machine name if DNS was unhelpful
                            if (isLocal && (string.IsNullOrEmpty(hostName) || hostName == ip))
                                hostName = Environment.MachineName;

                            var deviceType = isGateway
                                ? "Router"
                                : isLocal
                                    ? "This PC"
                                    : InferDeviceType(hostName, mfr);

                            var result = new NetworkScanResult
                            {
                                IpAddress = ip,
                                HostName = hostName,
                                MacAddress = mac,
                                Manufacturer = mfr,
                                DeviceType = deviceType,
                                Status = "Online",
                                RoundTripMs = reply.RoundtripTime,
                                IsLocalDevice = isLocal,
                                IsGateway = isGateway
                            };

                            Interlocked.Increment(ref found);
                            Dispatch(() =>
                            {
                                ScanResults.Add(result);
                                ScanStatus = string.Format(
                                    _loc.Get("net.scan_progress"),
                                    scanned, hostCount, ScanResults.Count);
                            });
                        }
                        else
                        {
                            Dispatch(() => ScanStatus = string.Format(
                                _loc.Get("net.scan_progress"),
                                scanned, hostCount, ScanResults.Count));
                        }
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (!ct.IsCancellationRequested)
                Dispatch(() => ScanStatus = string.Format(_loc.Get("net.scan_done"), ScanResults.Count, hostCount));
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            Log.Error("IP scan failed", ex);
            Dispatch(() => ScanStatus = _loc.Get("net.error"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    // ── ARP helper ─────────────────────────────────────────────────────────────
    // Runs `arp -a <ip>` and extracts the MAC address from output.
    // This is Windows-specific and intentional (the app targets Windows only).
    private static string GetMacFromArp(string ip)
    {
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "arp",
                Arguments = $"-a {ip}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains(ip)) continue;
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                // arp output: <ip>  <mac>  <type>
                if (parts.Length >= 2)
                {
                    var candidate = parts[1];
                    if (candidate.Contains('-') || candidate.Contains(':'))
                        return candidate.Replace('-', ':').ToUpperInvariant();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"ARP lookup failed for {ip}: {ex.Message}");
        }
        return string.Empty;
    }

    // ── NetBIOS name resolution ────────────────────────────────────────────────
    // Runs `nbtstat -A <ip>` and extracts the computer name from the output.
    // Windows-specific. Returns empty string on failure or timeout.
    private static string GetNetBiosName(string ip)
    {
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nbtstat",
                Arguments = $"-A {ip}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(); } catch { }
                return string.Empty;
            }

            // nbtstat output contains a table like:
            //   Name               Type         Status
            //   -----------------------------------------------
            //   DESKTOP-ABC  <00>  UNIQUE      Registered
            // The first <00> UNIQUE entry is the computer name.
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (!trimmed.Contains("<00>", StringComparison.OrdinalIgnoreCase)) continue;
                if (!trimmed.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)) continue;
                // Extract the name before <00>
                var idx = trimmed.IndexOf("<00>", StringComparison.OrdinalIgnoreCase);
                var name = trimmed[..idx].Trim();
                if (name.Length > 0 && !name.StartsWith("---") && !name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    return name;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"NetBIOS lookup failed for {ip}: {ex.Message}");
        }
        return string.Empty;
    }

    // ── OUI and device-type inference delegated to NetworkParsingHelpers ─────

    private static string LookupManufacturer(string mac) =>
        NetworkParsingHelpers.LookupManufacturer(mac);

    private static string InferDeviceType(string hostName, string manufacturer) =>
        NetworkParsingHelpers.InferDeviceType(hostName, manufacturer);


    /// <summary>
    /// Gets the local IPv4 address and subnet mask from the first active non-loopback
    /// interface that has a gateway configured (i.e. is actually connected to a router).
    /// Falls back to the first active unicast IPv4 if no gateway is found.
    /// </summary>
    private static (IPAddress? ip, IPAddress mask, IPAddress? gateway) GetLocalSubnet()
    {
        IPAddress? fallbackIp = null;
        IPAddress fallbackMask = IPAddress.Parse("255.255.255.0");

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
            var props = iface.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                var ip = addr.Address;
                var mask = addr.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
                // Prefer an interface that has a default gateway
                if (props.GatewayAddresses.Count > 0)
                {
                    var gw = props.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        ?.Address;
                    return (ip, mask, gw);
                }
                fallbackIp ??= ip;
                fallbackMask = mask;
            }
        }
        return (fallbackIp, fallbackMask, null);
    }

    private static string BuildIp(byte[] netBytes, int hostIndex)
    {
        var b = (byte[])netBytes.Clone();
        b[3] = (byte)(hostIndex & 0xFF);
        if (netBytes.Length == 4)
            b[2] = (byte)(netBytes[2] | (hostIndex >> 8 & 0xFF));
        return new IPAddress(b).ToString();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 3: TRACEROUTE
    // ═════════════════════════════════════════════════════════════════════════

    private string _traceHost = string.Empty;
    public string TraceHost
    {
        get => _traceHost;
        set => SetProperty(ref _traceHost, value);
    }

    private string _traceResult = string.Empty;
    public string TraceResult
    {
        get => _traceResult;
        private set => SetProperty(ref _traceResult, value);
    }

    public ICommand TraceCommand { get; }
    public ICommand CopyTraceCommand { get; }

    private async Task RunTraceAsync()
    {
        var host = _traceHost.Trim();
        if (string.IsNullOrEmpty(host)) return;
        if (!IsValidHostOrIp(host)) { TraceResult = _loc.Get("net.error"); return; }

        var ct = BeginOperation();
        TraceResult = _loc.Get("net.loading");
        try
        {
            var output = await Task.Run(() =>
            {
                using var proc = new System.Diagnostics.Process();
                proc.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "tracert",
                    Arguments = $"-d -w 2000 {host}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                proc.Start();
                var text = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(60_000);
                return text;
            }, ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested) return;
            Dispatch(() => TraceResult = string.IsNullOrWhiteSpace(output) ? _loc.Get("net.no_result") : output.Trim());
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            Log.Error("Traceroute failed", ex);
            Dispatch(() => TraceResult = _loc.Get("net.error"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 4: WHOIS
    // ═════════════════════════════════════════════════════════════════════════

    private string _whoisDomain = string.Empty;
    public string WhoisDomain
    {
        get => _whoisDomain;
        set => SetProperty(ref _whoisDomain, value);
    }

    private string _whoisResult = string.Empty;
    public string WhoisResult
    {
        get => _whoisResult;
        private set => SetProperty(ref _whoisResult, value);
    }

    public ICommand WhoisCommand { get; }
    public ICommand CopyWhoisCommand { get; }

    private async Task RunWhoisAsync()
    {
        var domain = _whoisDomain.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(domain)) return;

        // Strip protocol and path prefixes
        if (domain.StartsWith("http://", StringComparison.Ordinal)) domain = domain[7..];
        if (domain.StartsWith("https://", StringComparison.Ordinal)) domain = domain[8..];
        var slashPos = domain.IndexOf('/');
        if (slashPos > 0) domain = domain[..slashPos];
        if (!IsValidHostOrIp(domain)) { WhoisResult = _loc.Get("net.error"); return; }

        var ct = BeginOperation();
        WhoisResult = _loc.Get("net.loading");
        try
        {
            var raw = await QueryWhoisAsync(domain, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            Dispatch(() => WhoisResult = string.IsNullOrWhiteSpace(raw)
                ? _loc.Get("net.no_result")
                : FormatWhoisOutput(raw));
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            Log.Error("Whois failed", ex);
            Dispatch(() => WhoisResult = _loc.Get("net.offline"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    /// <summary>
    /// Queries whois.iana.org for the registrar endpoint, then queries that endpoint.
    /// Falls back to the IANA response alone if the registrar endpoint is unavailable.
    /// Uses raw TcpClient for the actual WHOIS protocol (port 43).
    /// </summary>
    private static async Task<string> QueryWhoisAsync(string domain, CancellationToken ct)
    {
        const string ianaServer = "whois.iana.org";
        var ianaRaw = await WhoisTcpAsync(ianaServer, domain, ct).ConfigureAwait(false);

        // Extract the "refer:" line from IANA to get the proper registrar WHOIS server
        string? referServer = null;
        foreach (var line in ianaRaw.Split('\n'))
        {
            var l = line.Trim();
            if (l.StartsWith("refer:", StringComparison.OrdinalIgnoreCase))
            {
                referServer = l[6..].Trim();
                break;
            }
            if (l.StartsWith("whois:", StringComparison.OrdinalIgnoreCase))
            {
                referServer = l[6..].Trim();
                break;
            }
        }

        if (!string.IsNullOrEmpty(referServer) && referServer != ianaServer)
        {
            try
            {
                return await WhoisTcpAsync(referServer, domain, ct).ConfigureAwait(false);
            }
            catch
            {
                // fallback to IANA response
            }
        }

        return ianaRaw;
    }

    private static async Task<string> WhoisTcpAsync(string server, string query, CancellationToken ct)
    {
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(server, 43, ct).ConfigureAwait(false);
        using var stream = client.GetStream();
        var request = System.Text.Encoding.ASCII.GetBytes(query + "\r\n");
        await stream.WriteAsync(request, ct).ConfigureAwait(false);

        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.ASCII);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Delegates whois output formatting to NetworkParsingHelpers.</summary>
    private static string FormatWhoisOutput(string raw) =>
        NetworkParsingHelpers.FormatWhoisOutput(raw);

    // ═════════════════════════════════════════════════════════════════════════
    // MODE 5: DNS LOOKUP
    // ═════════════════════════════════════════════════════════════════════════

    private string _dnsHost = string.Empty;
    public string DnsHost
    {
        get => _dnsHost;
        set => SetProperty(ref _dnsHost, value);
    }

    private string _dnsResult = string.Empty;
    public string DnsResult
    {
        get => _dnsResult;
        private set => SetProperty(ref _dnsResult, value);
    }

    public ICommand DnsCommand { get; }
    public ICommand CopyDnsCommand { get; }
    public ICommand OpenHelpCommand { get; }

    private async Task RunDnsAsync()
    {
        var host = _dnsHost.Trim();
        if (string.IsNullOrEmpty(host)) return;
        if (!IsValidHostOrIp(host)) { DnsResult = _loc.Get("net.error"); return; }

        var ct = BeginOperation();
        DnsResult = _loc.Get("net.loading");
        try
        {
            var entry = await Task.Run(() => Dns.GetHostEntry(host), ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            var lines = new List<string>
            {
                $"Hostname:  {entry.HostName}",
                string.Empty,
                "Addresses:"
            };
            foreach (var addr in entry.AddressList)
                lines.Add($"  {(addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "A    " : "AAAA ")}  {addr}");

            if (entry.Aliases.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Aliases:");
                foreach (var alias in entry.Aliases)
                    lines.Add($"  {alias}");
            }

            Dispatch(() => DnsResult = string.Join(Environment.NewLine, lines));
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (Exception ex)
        {
            Log.Error("DNS lookup failed", ex);
            Dispatch(() => DnsResult = ex is System.Net.Sockets.SocketException
                ? _loc.Get("net.dns_not_found")
                : _loc.Get("net.error"));
        }
        finally
        {
            Dispatch(EndOperation);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LABELS (localized)
    // ═════════════════════════════════════════════════════════════════════════

    public string ModeMyIpLabel { get; private set; } = string.Empty;
    public string ModePingLabel { get; private set; } = string.Empty;
    public string ModeScanLabel { get; private set; } = string.Empty;
    public string ModeTraceLabel { get; private set; } = string.Empty;
    public string ModeWhoisLabel { get; private set; } = string.Empty;
    public string ModeDnsLabel { get; private set; } = string.Empty;

    public string FetchLabel { get; private set; } = string.Empty;
    public string PingLabel { get; private set; } = string.Empty;
    public string ScanLabel { get; private set; } = string.Empty;
    public string TraceLabel { get; private set; } = string.Empty;
    public string WhoisLabel { get; private set; } = string.Empty;
    public string DnsLabel { get; private set; } = string.Empty;
    public string CopyLabel { get; private set; } = string.Empty;
    public string HostLabel { get; private set; } = string.Empty;
    public string CountLabel { get; private set; } = string.Empty;
    public string DomainLabel { get; private set; } = string.Empty;

    // IP Scanner column headers
    public string ColIpLabel { get; private set; } = string.Empty;
    public string ColHostLabel { get; private set; } = string.Empty;
    public string ColMacLabel { get; private set; } = string.Empty;
    public string ColMfrLabel { get; private set; } = string.Empty;
    public string ColTypeLabel { get; private set; } = string.Empty;
    public string ColStatusLabel { get; private set; } = string.Empty;
    public string ColRttLabel { get; private set; } = string.Empty;

    public void OnLanguageChanged() => RefreshLabels();

    private void RefreshLabels()
    {
        ModeMyIpLabel = _loc.Get("net.mode_myip");
        ModePingLabel = _loc.Get("net.mode_ping");
        ModeScanLabel = _loc.Get("net.mode_scan");
        ModeTraceLabel = _loc.Get("net.mode_trace");
        ModeWhoisLabel = _loc.Get("net.mode_whois");
        ModeDnsLabel = _loc.Get("net.mode_dns");

        FetchLabel = _loc.Get("net.fetch");
        PingLabel = _loc.Get("net.ping");
        ScanLabel = _loc.Get("net.scan");
        TraceLabel = _loc.Get("net.trace");
        WhoisLabel = _loc.Get("net.whois");
        DnsLabel = _loc.Get("net.dns");
        CopyLabel = _loc.Get("net.copy");
        HostLabel = _loc.Get("net.host");
        CountLabel = _loc.Get("net.count");
        DomainLabel = _loc.Get("net.domain");

        ColIpLabel = _loc.Get("net.col_ip");
        ColHostLabel = _loc.Get("net.col_host");
        ColMacLabel = _loc.Get("net.col_mac");
        ColMfrLabel = _loc.Get("net.col_mfr");
        ColTypeLabel = _loc.Get("net.col_type");
        ColStatusLabel = _loc.Get("net.col_status");
        ColRttLabel = _loc.Get("net.col_rtt");

        OnPropertyChanged(nameof(ModeMyIpLabel));
        OnPropertyChanged(nameof(ModePingLabel));
        OnPropertyChanged(nameof(ModeScanLabel));
        OnPropertyChanged(nameof(ModeTraceLabel));
        OnPropertyChanged(nameof(ModeWhoisLabel));
        OnPropertyChanged(nameof(ModeDnsLabel));
        OnPropertyChanged(nameof(FetchLabel));
        OnPropertyChanged(nameof(PingLabel));
        OnPropertyChanged(nameof(ScanLabel));
        OnPropertyChanged(nameof(TraceLabel));
        OnPropertyChanged(nameof(WhoisLabel));
        OnPropertyChanged(nameof(DnsLabel));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(HostLabel));
        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(DomainLabel));
        OnPropertyChanged(nameof(ColIpLabel));
        OnPropertyChanged(nameof(ColHostLabel));
        OnPropertyChanged(nameof(ColMacLabel));
        OnPropertyChanged(nameof(ColMfrLabel));
        OnPropertyChanged(nameof(ColTypeLabel));
        OnPropertyChanged(nameof(ColStatusLabel));
        OnPropertyChanged(nameof(ColRttLabel));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    public NetworkToolsViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

        SetModeMyIpCommand  = new RelayCommand(() => ActiveMode = 0);
        SetModePingCommand  = new RelayCommand(() => ActiveMode = 1);
        SetModeScanCommand  = new RelayCommand(() => ActiveMode = 2);
        SetModeTraceCommand = new RelayCommand(() => ActiveMode = 3);
        SetModeWhoisCommand = new RelayCommand(() => ActiveMode = 4);
        SetModeDnsCommand   = new RelayCommand(() => ActiveMode = 5);

        FetchMyIpCommand = new RelayCommand(() => _ = FetchMyIpAsync(), () => IsNotBusy);
        CopyMyIpCommand = new RelayCommand(
            () => System.Windows.Clipboard.SetText(_myIpResult),
            () => !string.IsNullOrEmpty(_myIpResult));

        PingCommand = new RelayCommand(
            () => _ = RunPingAsync(),
            () => IsNotBusy && !string.IsNullOrWhiteSpace(_pingHost));
        CopyPingCommand = new RelayCommand(
            () => System.Windows.Clipboard.SetText(_pingResult),
            () => !string.IsNullOrEmpty(_pingResult));

        ScanCommand = new RelayCommand(() => _ = RunScanAsync(), () => IsNotBusy);

        TraceCommand = new RelayCommand(
            () => _ = RunTraceAsync(),
            () => IsNotBusy && !string.IsNullOrWhiteSpace(_traceHost));
        CopyTraceCommand = new RelayCommand(
            () => System.Windows.Clipboard.SetText(_traceResult),
            () => !string.IsNullOrEmpty(_traceResult));

        WhoisCommand = new RelayCommand(
            () => _ = RunWhoisAsync(),
            () => IsNotBusy && !string.IsNullOrWhiteSpace(_whoisDomain));
        CopyWhoisCommand = new RelayCommand(
            () => System.Windows.Clipboard.SetText(_whoisResult),
            () => !string.IsNullOrEmpty(_whoisResult));

        DnsCommand = new RelayCommand(
            () => _ = RunDnsAsync(),
            () => IsNotBusy && !string.IsNullOrWhiteSpace(_dnsHost));
        CopyDnsCommand = new RelayCommand(
            () => System.Windows.Clipboard.SetText(_dnsResult),
            () => !string.IsNullOrEmpty(_dnsResult));
        OpenHelpCommand = new RelayCommand<string>(key =>
        {
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });

        RefreshLabels();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // DISPATCH HELPER
    // ═════════════════════════════════════════════════════════════════════════

    private static void Dispatch(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.Invoke(action);
        else
            action();
    }
}
