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
        set
        {
            if (SetProperty(ref _selectedWifiNetwork, value))
            {
                // Always reset to the network's stored password (empty if not
                // retrievable). Keeps old password from persisting when switching
                // to a network whose key couldn't be read without elevation.
                WifiPassword = value?.Password ?? string.Empty;
                WifiPasswordVisible = false;
            }
        }
    }

    private string _wifiPassword = string.Empty;
    public string WifiPassword
    {
        get => _wifiPassword;
        set
        {
            if (SetProperty(ref _wifiPassword, value))
                OnPropertyChanged(nameof(WifiPasswordMasked));
        }
    }

    private bool _wifiIsHidden;
    public bool WifiIsHidden
    {
        get => _wifiIsHidden;
        set => SetProperty(ref _wifiIsHidden, value);
    }

    private bool _wifiPasswordVisible;
    public bool WifiPasswordVisible
    {
        get => _wifiPasswordVisible;
        set => SetProperty(ref _wifiPasswordVisible, value);
    }

    // Bullet-masked version of the password for the hidden state.
    public string WifiPasswordMasked => new string('\u2022', _wifiPassword.Length);

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

    // ── Decode mode ───────────────────────────────────────────────────────────

    private bool _isModeDecode;
    public bool IsModeDecode
    {
        get => _isModeDecode;
        set
        {
            if (SetProperty(ref _isModeDecode, value))
                OnPropertyChanged(nameof(IsModeGenerate));
        }
    }

    public bool IsModeGenerate
    {
        get => !_isModeDecode;
        set { if (value) IsModeDecode = false; }
    }

    private string _decodeResult = string.Empty;
    public string DecodeResult
    {
        get => _decodeResult;
        private set
        {
            if (SetProperty(ref _decodeResult, value))
                OnPropertyChanged(nameof(HasDecodeResult));
        }
    }

    public bool HasDecodeResult => !string.IsNullOrEmpty(_decodeResult);

    // ── Localization labels ───────────────────────────────────────────────────

    public string TypeTextUrlLabel     { get; private set; } = string.Empty;
    public string TypeWifiLabel        { get; private set; } = string.Empty;
    public string TypeVCardLabel       { get; private set; } = string.Empty;
    public string ModeGenerateLabel    { get; private set; } = string.Empty;
    public string ModeDecodeLabel      { get; private set; } = string.Empty;
    public string InputLabel           { get; private set; } = string.Empty;
    public string InputHint            { get; private set; } = string.Empty;
    public string WifiNetworkLabel     { get; private set; } = string.Empty;
    public string WifiPasswordLabel    { get; private set; } = string.Empty;
    public string WifiPasswordHint     { get; private set; } = string.Empty;
    public string WifiPasswordTooltip  { get; private set; } = string.Empty;
    public string WifiPasswordCopiedLabel { get; private set; } = string.Empty;
    public string WifiRefreshLabel     { get; private set; } = string.Empty;
    public string WifiNoNetworksLabel  { get; private set; } = string.Empty;
    public string WifiHiddenLabel      { get; private set; } = string.Empty;
    public string VCardFirstLabel      { get; private set; } = string.Empty;
    public string VCardLastLabel       { get; private set; } = string.Empty;
    public string VCardPhoneLabel      { get; private set; } = string.Empty;
    public string VCardEmailLabel      { get; private set; } = string.Empty;
    public string GenerateLabel        { get; private set; } = string.Empty;
    public string SaveLabel            { get; private set; } = string.Empty;
    public string CopyLabel            { get; private set; } = string.Empty;
    public string NoContentLabel       { get; private set; } = string.Empty;
    public string CopiedLabel          { get; private set; } = string.Empty;
    public string SaveTitleLabel       { get; private set; } = string.Empty;
    public string DecodeBrowseLabel    { get; private set; } = string.Empty;
    public string DecodeHintLabel      { get; private set; } = string.Empty;
    public string DecodeResultLabel    { get; private set; } = string.Empty;
    public string DecodeNoQrLabel      { get; private set; } = string.Empty;
    public string DecodeCopyLabel      { get; private set; } = string.Empty;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand GenerateCommand              { get; }
    public ICommand SaveCommand                  { get; }
    public ICommand CopyToClipboardCommand       { get; }
    public ICommand LoadWifiNetworksCommand      { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }
    public ICommand CopyWifiPasswordCommand      { get; }
    public ICommand BrowseDecodeImageCommand     { get; }
    public ICommand CopyDecodeResultCommand      { get; }

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
        TogglePasswordVisibilityCommand = new RelayCommand(() => WifiPasswordVisible = !_wifiPasswordVisible);
        CopyWifiPasswordCommand  = new RelayCommand(DoCopyWifiPassword);
        BrowseDecodeImageCommand = new RelayCommand(DoBrowseDecodeImage);
        CopyDecodeResultCommand  = new RelayCommand(DoCopyDecodeResult);

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

        // Standard WIFI QR URI scheme - recognized by iOS Camera and Android.
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

    // pixels-per-module fixed at 20 - renders at the 240 px preview cap with good clarity
    private const int Ppm = 20;

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
            var gen     = new QRCodeGenerator();
            var data    = gen.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var bmpCode = new BitmapByteQRCode(data);

            // Always black-on-white regardless of app theme: scanners require this.
            byte[] bmpBytes = bmpCode.GetGraphic(Ppm, "#000000", "#ffffff");

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

    // ── Copy QR image ─────────────────────────────────────────────────────────

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

    // ── Copy WiFi password ────────────────────────────────────────────────────

    private DispatcherTimer? _pwCopyResetTimer;

    private void DoCopyWifiPassword()
    {
        if (string.IsNullOrEmpty(_wifiPassword)) return;
        try
        {
            System.Windows.Clipboard.SetText(_wifiPassword);
            StatusMessage = WifiPasswordCopiedLabel;

            _pwCopyResetTimer?.Stop();
            _pwCopyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pwCopyResetTimer.Tick += (_, _) =>
            {
                _pwCopyResetTimer!.Stop();
                StatusMessage = string.Empty;
            };
            _pwCopyResetTimer.Start();
        }
        catch { }
    }

    // ── Decode ────────────────────────────────────────────────────────────────

    private string _decodeFilePath = string.Empty;

    // Called from code-behind when a file is dropped onto the decode panel.
    public void DecodeQrFromPath(string filePath)
    {
        _decodeFilePath = filePath;
        DecodeResult = string.Empty;
        DoDecodeQr();
    }

    private void DoBrowseDecodeImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = DecodeBrowseLabel,
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        DecodeQrFromPath(dlg.FileName);
    }

    private void DoDecodeQr()
    {
        if (string.IsNullOrEmpty(_decodeFilePath)) return;
        try
        {
            // Load via WPF - handles PNG, JPG, BMP, GIF, TIFF without System.Drawing.
            var fileDecoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                new Uri(_decodeFilePath, UriKind.Absolute),
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            var frame = fileDecoder.Frames[0];

            // Convert to BGRA32 so ZXing's RGBLuminanceSource gets a known pixel layout.
            var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);

            int    width  = converted.PixelWidth;
            int    height = converted.PixelHeight;
            int    stride = width * 4;
            byte[] pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            var luminance    = new ZXing.RGBLuminanceSource(
                pixels, width, height, ZXing.RGBLuminanceSource.BitmapFormat.BGRA32);
            var binaryBitmap = new ZXing.BinaryBitmap(
                new ZXing.Common.HybridBinarizer(luminance));

            ZXing.Result? result;
            try
            {
                result = new ZXing.QrCode.QRCodeReader().decode(
                    binaryBitmap,
                    new Dictionary<ZXing.DecodeHintType, object>
                    {
                        [ZXing.DecodeHintType.TRY_HARDER] = true
                    });
            }
            catch
            {
                result = null;
            }

            if (result is not null && !string.IsNullOrEmpty(result.Text))
            {
                DecodeResult = result.Text;
                StatusMessage = string.Empty;
            }
            else
            {
                DecodeResult = string.Empty;
                StatusMessage = DecodeNoQrLabel;
                _copyResetTimer?.Stop();
                _copyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _copyResetTimer.Tick += (_, _) => { _copyResetTimer!.Stop(); StatusMessage = string.Empty; };
                _copyResetTimer.Start();
            }
        }
        catch (Exception ex)
        {
            DecodeResult = ex.Message;
        }
    }

    private void DoCopyDecodeResult()
    {
        if (string.IsNullOrEmpty(_decodeResult)) return;
        try
        {
            System.Windows.Clipboard.SetText(_decodeResult);
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
        ModeGenerateLabel   = _loc.Get("qr.mode_generate");
        ModeDecodeLabel     = _loc.Get("qr.mode_decode");
        InputLabel          = _loc.Get("qr.input_label");
        InputHint           = _loc.Get("qr.input_hint");
        WifiNetworkLabel    = _loc.Get("qr.wifi_network");
        WifiPasswordLabel   = _loc.Get("qr.wifi_password");
        WifiPasswordHint    = _loc.Get("qr.wifi_password_hint");
        WifiPasswordTooltip = _loc.Get("qr.wifi_password_tooltip");
        WifiPasswordCopiedLabel = _loc.Get("qr.wifi_password_copied");
        WifiRefreshLabel    = _loc.Get("qr.wifi_refresh");
        WifiNoNetworksLabel = _loc.Get("qr.wifi_no_networks");
        WifiHiddenLabel     = _loc.Get("qr.wifi_hidden");
        VCardFirstLabel     = _loc.Get("qr.vcard_first");
        VCardLastLabel      = _loc.Get("qr.vcard_last");
        VCardPhoneLabel     = _loc.Get("qr.vcard_phone");
        VCardEmailLabel     = _loc.Get("qr.vcard_email");
        GenerateLabel       = _loc.Get("qr.generate");
        SaveLabel           = _loc.Get("qr.save");
        CopyLabel           = _loc.Get("qr.copy");
        NoContentLabel      = _loc.Get("qr.no_content");
        CopiedLabel         = _loc.Get("qr.copied");
        SaveTitleLabel      = _loc.Get("qr.save_title");
        DecodeBrowseLabel   = _loc.Get("qr.decode_browse");
        DecodeHintLabel     = _loc.Get("qr.decode_hint");
        DecodeResultLabel   = _loc.Get("qr.decode_result_label");
        DecodeNoQrLabel     = _loc.Get("qr.decode_no_qr");
        DecodeCopyLabel     = _loc.Get("qr.decode_copy");

        OnPropertyChanged(nameof(TypeTextUrlLabel));
        OnPropertyChanged(nameof(TypeWifiLabel));
        OnPropertyChanged(nameof(TypeVCardLabel));
        OnPropertyChanged(nameof(ModeGenerateLabel));
        OnPropertyChanged(nameof(ModeDecodeLabel));
        OnPropertyChanged(nameof(InputLabel));
        OnPropertyChanged(nameof(InputHint));
        OnPropertyChanged(nameof(WifiNetworkLabel));
        OnPropertyChanged(nameof(WifiPasswordLabel));
        OnPropertyChanged(nameof(WifiPasswordHint));
        OnPropertyChanged(nameof(WifiPasswordTooltip));
        OnPropertyChanged(nameof(WifiPasswordCopiedLabel));
        OnPropertyChanged(nameof(WifiRefreshLabel));
        OnPropertyChanged(nameof(WifiNoNetworksLabel));
        OnPropertyChanged(nameof(WifiHiddenLabel));
        OnPropertyChanged(nameof(VCardFirstLabel));
        OnPropertyChanged(nameof(VCardLastLabel));
        OnPropertyChanged(nameof(VCardPhoneLabel));
        OnPropertyChanged(nameof(VCardEmailLabel));
        OnPropertyChanged(nameof(GenerateLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(CopyLabel));
        OnPropertyChanged(nameof(NoContentLabel));
        OnPropertyChanged(nameof(CopiedLabel));
        OnPropertyChanged(nameof(SaveTitleLabel));
        OnPropertyChanged(nameof(DecodeBrowseLabel));
        OnPropertyChanged(nameof(DecodeHintLabel));
        OnPropertyChanged(nameof(DecodeResultLabel));
        OnPropertyChanged(nameof(DecodeNoQrLabel));
        OnPropertyChanged(nameof(DecodeCopyLabel));
    }
}
