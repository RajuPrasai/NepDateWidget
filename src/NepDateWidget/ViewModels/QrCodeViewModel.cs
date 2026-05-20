using NepDateWidget.Helpers;
using NepDateWidget.Services;
using QRCoder;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Singleton ViewModel for the QR Code generator sub-view.
/// Supports three input modes: plain Text/URL, WiFi (WIFI: URI scheme for
/// mobile auto-connect), and vCard contact.  Output is always black-on-white
/// so any scanner can read it regardless of app theme.
/// </summary>
public sealed class QrCodeViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;

    // ── QR type (0=Text/URL, 1=WiFi, 2=vCard) ────────────────────────────────

    private int _qrTypeIndex;
    public int QrTypeIndex
    {
        get => _qrTypeIndex;
        set
        {
            if (SetProperty(ref _qrTypeIndex, value))
            {
                OnPropertyChanged(nameof(IsTypeTextUrl));
                OnPropertyChanged(nameof(IsTypeWifi));
                OnPropertyChanged(nameof(IsTypeVCard));
            }
        }
    }

    public bool IsTypeTextUrl { get => _qrTypeIndex == 0; set { if (value) QrTypeIndex = 0; } }
    public bool IsTypeWifi    { get => _qrTypeIndex == 1; set { if (value) QrTypeIndex = 1; } }
    public bool IsTypeVCard   { get => _qrTypeIndex == 2; set { if (value) QrTypeIndex = 2; } }

    // ── Text / URL ────────────────────────────────────────────────────────────

    private string _textInput = string.Empty;
    public string TextInput
    {
        get => _textInput;
        set => SetProperty(ref _textInput, value);
    }

    // ── WiFi ──────────────────────────────────────────────────────────────────

    public ObservableCollection<WifiProfile> WifiNetworks { get; } = new();

    private WifiProfile? _selectedWifiNetwork;
    public WifiProfile? SelectedWifiNetwork
    {
        get => _selectedWifiNetwork;
        set => SetProperty(ref _selectedWifiNetwork, value);
    }

    private string _wifiPassword = string.Empty;
    public string WifiPassword
    {
        get => _wifiPassword;
        set => SetProperty(ref _wifiPassword, value);
    }

    private bool _wifiIsHidden;
    public bool WifiIsHidden
    {
        get => _wifiIsHidden;
        set => SetProperty(ref _wifiIsHidden, value);
    }

    private bool _isLoadingWifi;
    public bool IsLoadingWifi
    {
        get => _isLoadingWifi;
        private set => SetProperty(ref _isLoadingWifi, value);
    }

    public bool HasWifiNetworks => WifiNetworks.Count > 0;

    // ── vCard ─────────────────────────────────────────────────────────────────

    private string _vCardFirstName = string.Empty;
    public string VCardFirstName
    {
        get => _vCardFirstName;
        set => SetProperty(ref _vCardFirstName, value);
    }

    private string _vCardLastName = string.Empty;
    public string VCardLastName
    {
        get => _vCardLastName;
        set => SetProperty(ref _vCardLastName, value);
    }

    private string _vCardPhone = string.Empty;
    public string VCardPhone
    {
        get => _vCardPhone;
        set => SetProperty(ref _vCardPhone, value);
    }

    private string _vCardEmail = string.Empty;
    public string VCardEmail
    {
        get => _vCardEmail;
        set => SetProperty(ref _vCardEmail, value);
    }

    // ── Output ────────────────────────────────────────────────────────────────

    // 0=tiny, 1=small, 2=medium (default), 3=large, 4=huge
    private int _qrSizeIndex = 2;
    public int QrSizeIndex
    {
        get => _qrSizeIndex;
        set => SetProperty(ref _qrSizeIndex, Math.Clamp(value, 0, 4));
    }

    private BitmapSource? _generatedQrBitmap;
    public BitmapSource? GeneratedQrBitmap
    {
        get => _generatedQrBitmap;
        private set
        {
            if (SetProperty(ref _generatedQrBitmap, value))
                OnPropertyChanged(nameof(HasQrBitmap));
        }
    }

    public bool HasQrBitmap => _generatedQrBitmap is not null;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    // ── Localization labels ───────────────────────────────────────────────────

    public string TypeTextUrlLabel     { get; private set; } = string.Empty;
    public string TypeWifiLabel        { get; private set; } = string.Empty;
    public string TypeVCardLabel       { get; private set; } = string.Empty;
    public string InputLabel           { get; private set; } = string.Empty;
    public string InputHint            { get; private set; } = string.Empty;
    public string WifiNetworkLabel     { get; private set; } = string.Empty;
    public string WifiPasswordLabel    { get; private set; } = string.Empty;
    public string WifiPasswordHint     { get; private set; } = string.Empty;
    public string WifiPasswordTooltip  { get; private set; } = string.Empty;
    public string WifiRefreshLabel     { get; private set; } = string.Empty;
    public string WifiNoNetworksLabel  { get; private set; } = string.Empty;
    public string WifiHiddenLabel      { get; private set; } = string.Empty;
    public string VCardFirstLabel      { get; private set; } = string.Empty;
    public string VCardLastLabel       { get; private set; } = string.Empty;
    public string VCardPhoneLabel      { get; private set; } = string.Empty;
    public string VCardEmailLabel      { get; private set; } = string.Empty;
    public string SizeLabel            { get; private set; } = string.Empty;
    public string GenerateLabel        { get; private set; } = string.Empty;
    public string SaveLabel            { get; private set; } = string.Empty;
    public string CopyLabel            { get; private set; } = string.Empty;
    public string NoContentLabel       { get; private set; } = string.Empty;
    public string CopiedLabel          { get; private set; } = string.Empty;
    public string SaveTitleLabel       { get; private set; } = string.Empty;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand GenerateCommand          { get; }
    public ICommand SaveCommand              { get; }
    public ICommand CopyToClipboardCommand   { get; }
    public ICommand LoadWifiNetworksCommand  { get; }

    // ── Construction ─────────────────────────────────────────────────────────

    public QrCodeViewModel(ILocalizationService loc)
    {
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));

        // Commands with no canExecute: button availability controlled via
        // IsEnabled="{Binding HasQrBitmap}" in XAML to avoid RequerySuggested overhead.
        GenerateCommand         = new RelayCommand(DoGenerate);
        SaveCommand             = new RelayCommand(DoSave);
        CopyToClipboardCommand  = new RelayCommand(DoCopy);
        LoadWifiNetworksCommand = new RelayCommand(DoLoadWifiNetworks);

        RefreshLabels();
        DoLoadWifiNetworks();
    }

    public void OnLanguageChanged() => RefreshLabels();

    // ── QR content builders ───────────────────────────────────────────────────

    private string BuildQrContent()
    {
        return _qrTypeIndex switch
        {
            1 => BuildWifiString(),
            2 => BuildVCardString(),
            _ => _textInput.Trim()
        };
    }

    private string BuildWifiString()
    {
        if (_selectedWifiNetwork is null) return string.Empty;

        string ssid = EscapeWifi(_selectedWifiNetwork.Ssid);
        string pass = EscapeWifi(_wifiPassword);
        string auth = _selectedWifiNetwork.QrAuthType;
        string hidden = _wifiIsHidden ? "true" : "false";

        // Standard WIFI QR URI scheme — recognized by iOS Camera and Android.
        // Auth values: WPA (covers WPA/WPA2/WPA3), WEP, nopass.
        return $"WIFI:T:{auth};S:{ssid};P:{pass};H:{hidden};;";
    }

    private string BuildVCardString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        string fn = $"{_vCardFirstName} {_vCardLastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fn))
            sb.AppendLine($"FN:{fn}");
        sb.AppendLine($"N:{_vCardLastName};{_vCardFirstName};;;");
        if (!string.IsNullOrWhiteSpace(_vCardPhone))
            sb.AppendLine($"TEL:{_vCardPhone}");
        if (!string.IsNullOrWhiteSpace(_vCardEmail))
            sb.AppendLine($"EMAIL:{_vCardEmail}");
        sb.AppendLine("END:VCARD");
        return sb.ToString();
    }

    // Escape special characters per the WiFi QR spec: \  ;  ,  "  :
    private static string EscapeWifi(string value) =>
        value.Replace("\\", "\\\\")
             .Replace(";",  "\\;")
             .Replace(",",  "\\,")
             .Replace("\"", "\\\"")
             .Replace(":",  "\\:");

    // ── Generate ──────────────────────────────────────────────────────────────

    // pixels-per-module mapped to size index 0–4
    private static readonly int[] _ppm = [4, 7, 12, 20, 32];

    private void DoGenerate()
    {
        string content = BuildQrContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusMessage = NoContentLabel;
            return;
        }

        try
        {
            int ppm = _ppm[Math.Clamp(_qrSizeIndex, 0, 4)];

            var gen     = new QRCodeGenerator();
            var data    = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var bmpCode = new BitmapByteQRCode(data);

            // Always black-on-white regardless of app theme: scanners require this.
            byte[] bmpBytes = bmpCode.GetGraphic(ppm, "#000000", "#ffffff");

            using var ms = new MemoryStream(bmpBytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();

            GeneratedQrBitmap = img;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    private void DoSave()
    {
        if (_generatedQrBitmap is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = SaveTitleLabel,
            FileName = "qrcode.png",
            Filter   = "PNG image|*.png"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_generatedQrBitmap));
            using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    // ── Copy ─────────────────────────────────────────────────────────────────

    private DispatcherTimer? _copyResetTimer;

    private void DoCopy()
    {
        if (_generatedQrBitmap is null) return;
        try
        {
            System.Windows.Clipboard.SetImage(_generatedQrBitmap);
            StatusMessage = CopiedLabel;

            _copyResetTimer?.Stop();
            _copyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _copyResetTimer.Tick += (_, _) =>
            {
                _copyResetTimer!.Stop();
                StatusMessage = string.Empty;
            };
            _copyResetTimer.Start();
        }
        catch { }
    }

    // ── WiFi network loading ──────────────────────────────────────────────────

    private void DoLoadWifiNetworks()
    {
        if (_isLoadingWifi) return;
        IsLoadingWifi = true;

        Task.Run(static () =>
        {
            try { return WifiNetworkScanner.Scan(); }
            catch { return (new List<WifiProfile>(), string.Empty); }
        }).ContinueWith(t =>
        {
            var (profiles, connectedSsid) = t.Result;

            WifiNetworks.Clear();
            foreach (var p in profiles)
                WifiNetworks.Add(p);

            // Auto-select the currently connected network; fall back to first entry.
            SelectedWifiNetwork = (!string.IsNullOrEmpty(connectedSsid)
                ? WifiNetworks.FirstOrDefault(p => p.Ssid == connectedSsid)
                : null)
                ?? (WifiNetworks.Count > 0 ? WifiNetworks[0] : null);

            OnPropertyChanged(nameof(HasWifiNetworks));
            IsLoadingWifi = false;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    private void RefreshLabels()
    {
        TypeTextUrlLabel    = _loc.Get("qr.type_text");
        TypeWifiLabel       = _loc.Get("qr.type_wifi");
        TypeVCardLabel      = _loc.Get("qr.type_vcard");
        InputLabel          = _loc.Get("qr.input_label");
        InputHint           = _loc.Get("qr.input_hint");
        WifiNetworkLabel    = _loc.Get("qr.wifi_network");
        WifiPasswordLabel   = _loc.Get("qr.wifi_password");
        WifiPasswordHint    = _loc.Get("qr.wifi_password_hint");
        WifiPasswordTooltip = _loc.Get("qr.wifi_password_tooltip");
        WifiRefreshLabel    = _loc.Get("qr.wifi_refresh");
        WifiNoNetworksLabel = _loc.Get("qr.wifi_no_networks");
        WifiHiddenLabel     = _loc.Get("qr.wifi_hidden");
        VCardFirstLabel     = _loc.Get("qr.vcard_first");
        VCardLastLabel      = _loc.Get("qr.vcard_last");
        VCardPhoneLabel     = _loc.Get("qr.vcard_phone");
        VCardEmailLabel     = _loc.Get("qr.vcard_email");
        SizeLabel           = _loc.Get("qr.size_label");
        GenerateLabel       = _loc.Get("qr.generate");
        SaveLabel           = _loc.Get("qr.save");
        CopyLabel           = _loc.Get("qr.copy");
        NoContentLabel      = _loc.Get("qr.no_content");
        CopiedLabel         = _loc.Get("qr.copied");
        SaveTitleLabel      = _loc.Get("qr.save_title");

        OnPropertyChanged(nameof(TypeTextUrlLabel));
        OnPropertyChanged(nameof(TypeWifiLabel));
        OnPropertyChanged(nameof(TypeVCardLabel));
        OnPropertyChanged(nameof(InputLabel));
        OnPropertyChanged(nameof(InputHint));
        OnPropertyChanged(nameof(WifiNetworkLabel));
        OnPropertyChanged(nameof(WifiPasswordLabel));
        OnPropertyChanged(nameof(WifiPasswordHint));
        OnPropertyChanged(nameof(WifiPasswordTooltip));
        OnPropertyChanged(nameof(WifiRefreshLabel));
        OnPropertyChanged(nameof(WifiNoNetworksLabel));
        OnPropertyChanged(nameof(WifiHiddenLabel));
        OnPropertyChanged(nameof(VCardFirstLabel));
        OnPropertyChanged(nameof(VCardLastLabel));
        OnPropertyChanged(nameof(VCardPhoneLabel));
        OnPropertyChanged(nameof(VCardEmailLabel));
        OnPropertyChanged(nameof(SizeLabel));
        OnPropertyChanged(nameof(GenerateLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(NoContentLabel));
        OnPropertyChanged(nameof(CopiedLabel));
        OnPropertyChanged(nameof(SaveTitleLabel));
    }
}
