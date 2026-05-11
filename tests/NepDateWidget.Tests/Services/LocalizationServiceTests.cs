using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

public class LocalizationServiceTests
{
    private static LocalizationService Create(string lang = "en")
    {
        var svc = new LocalizationService();
        svc.SetLanguage(lang);
        return svc;
    }

    // ── Known keys ────────────────────────────────────────────────────────────

    [Fact]
    public void Get_KnownEnglishKey_ReturnsEnglishText()
    {
        var svc = Create("en");
        var v   = svc.Get("app.exit");
        Assert.Equal("Exit", v);
    }

    [Fact]
    public void Get_KnownNepaliKey_ReturnsNepaliText()
    {
        var svc = Create("ne");
        var v   = svc.Get("app.exit");
        Assert.Equal("बन्द गर्नुहोस्", v);
    }

    // ── Language switching ────────────────────────────────────────────────────

    [Fact]
    public void SetLanguage_SwitchesToNepali_UpdatesGet()
    {
        var svc = Create("en");
        svc.SetLanguage("ne");
        Assert.Equal("ne", svc.CurrentLanguage);
        Assert.NotEqual(Create("en").Get("app.name"), svc.Get("app.name"));
    }

    [Fact]
    public void SetLanguage_CaseInsensitive_Accepted()
    {
        var svc = Create("EN");
        Assert.Equal("en", svc.CurrentLanguage);
    }

    // ── Fallback ──────────────────────────────────────────────────────────────

    [Fact]
    public void Get_MissingKey_ReturnsKeyInBrackets()
    {
        var svc = Create("en");
        var v   = svc.Get("this.key.does.not.exist");
        Assert.Equal("[this.key.does.not.exist]", v);
    }

    [Fact]
    public void Get_NullKey_ReturnsKeyInBrackets()
    {
        var svc = Create("en");
        var v   = svc.Get(null!);
        Assert.Equal("[]", v);
    }

    // ── Day of week keys ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("dow.sun"), InlineData("dow.mon"), InlineData("dow.tue"),
     InlineData("dow.wed"), InlineData("dow.thu"), InlineData("dow.fri"),
     InlineData("dow.sat")]
    public void Get_DayOfWeekKeys_AllReturnNonEmpty(string key)
    {
        var svc = Create("en");
        Assert.NotEmpty(svc.Get(key));
    }

    [Theory]
    [InlineData("dow.sun"), InlineData("dow.mon"), InlineData("dow.tue"),
     InlineData("dow.wed"), InlineData("dow.thu"), InlineData("dow.fri"),
     InlineData("dow.sat")]
    public void Get_DayOfWeekKeys_NepaliAllReturnNonEmpty(string key)
    {
        var svc = Create("ne");
        Assert.NotEmpty(svc.Get(key));
    }

    // ── All mandatory keys exist ──────────────────────────────────────────────

    [Theory]
    [InlineData("app.name"), InlineData("app.exit"), InlineData("app.settings"),
     InlineData("minibar.today"), InlineData("minibar.expand_hint"),
     InlineData("converter.title"), InlineData("converter.convert_btn"),
     InlineData("menu.always_on_top"), InlineData("menu.exit"),
     InlineData("menu.theme_dark"), InlineData("menu.theme_light")]
    public void Get_MandatoryKeys_ArePresent_EnAndNe(string key)
    {
        Assert.NotEqual($"[{key}]", Create("en").Get(key));
        Assert.NotEqual($"[{key}]", Create("ne").Get(key));
    }

    // ── Context menu keys (added in M8) ───────────────────────────────────────

    [Theory]
    [InlineData("menu.language"),
     InlineData("menu.always_on_top"),
     InlineData("menu.show_clock"),
     InlineData("menu.show_timezone"),
     InlineData("menu.exit")]
    public void Get_MenuKeys_NonEmpty_InBothLanguages(string key)
    {
        Assert.NotEqual($"[{key}]", Create("en").Get(key));
        Assert.NotEqual($"[{key}]", Create("ne").Get(key));
    }

    // ── Network Tools tab keys ────────────────────────────────────────────────

    [Theory]
    [InlineData("tab.network")]
    [InlineData("net.mode_myip")]
    [InlineData("net.mode_ping")]
    [InlineData("net.mode_scan")]
    [InlineData("net.mode_trace")]
    [InlineData("net.mode_whois")]
    [InlineData("net.mode_dns")]
    [InlineData("net.fetch")]
    [InlineData("net.ping")]
    [InlineData("net.scan")]
    [InlineData("net.trace")]
    [InlineData("net.whois")]
    [InlineData("net.dns")]
    [InlineData("net.copy")]
    [InlineData("net.host")]
    [InlineData("net.count")]
    [InlineData("net.domain")]
    [InlineData("net.loading")]
    [InlineData("net.offline")]
    [InlineData("net.error")]
    [InlineData("net.no_result")]
    [InlineData("net.dns_not_found")]
    [InlineData("net.no_network")]
    [InlineData("net.scanning")]
    [InlineData("net.scan_progress")]
    [InlineData("net.scan_done")]
    [InlineData("net.col_ip")]
    [InlineData("net.col_host")]
    [InlineData("net.col_mac")]
    [InlineData("net.col_mfr")]
    [InlineData("net.col_type")]
    [InlineData("net.col_status")]
    [InlineData("net.col_rtt")]
    public void Get_NetworkKeys_PresentInBothLanguages(string key)
    {
        Assert.NotEqual($"[{key}]", Create("en").Get(key));
        Assert.NotEqual($"[{key}]", Create("ne").Get(key));
    }

    [Theory]
    [InlineData("net.mode_myip")]
    [InlineData("net.mode_ping")]
    [InlineData("net.mode_scan")]
    [InlineData("net.mode_trace")]
    [InlineData("net.mode_whois")]
    [InlineData("net.mode_dns")]
    public void Get_NetworkModeKeys_NonEmpty_InEnglish(string key)
    {
        Assert.NotEmpty(Create("en").Get(key));
    }

    [Theory]
    [InlineData("net.mode_myip")]
    [InlineData("net.mode_ping")]
    [InlineData("net.mode_scan")]
    [InlineData("net.mode_trace")]
    [InlineData("net.mode_whois")]
    [InlineData("net.mode_dns")]
    public void Get_NetworkModeKeys_NonEmpty_InNepali(string key)
    {
        Assert.NotEmpty(Create("ne").Get(key));
    }

    [Theory]
    [InlineData("net.col_ip")]
    [InlineData("net.col_host")]
    [InlineData("net.col_mac")]
    [InlineData("net.col_mfr")]
    [InlineData("net.col_type")]
    [InlineData("net.col_status")]
    [InlineData("net.col_rtt")]
    public void Get_ScannerColumnHeaders_NonEmpty_InBothLanguages(string key)
    {
        Assert.NotEmpty(Create("en").Get(key));
        Assert.NotEmpty(Create("ne").Get(key));
    }

    [Fact]
    public void Get_NetworkLoading_NonEmpty_InBothLanguages()
    {
        Assert.NotEmpty(Create("en").Get("net.loading"));
        Assert.NotEmpty(Create("ne").Get("net.loading"));
    }

    [Fact]
    public void Get_NetworkOffline_NonEmpty_InBothLanguages()
    {
        Assert.NotEmpty(Create("en").Get("net.offline"));
        Assert.NotEmpty(Create("ne").Get("net.offline"));
    }

    [Fact]
    public void Get_NetworkScanProgress_ContainsPlaceholders()
    {
        // The progress string uses {0}, {1}, {2} for string.Format
        var v = Create("en").Get("net.scan_progress");
        Assert.Contains("{0}", v);
        Assert.Contains("{1}", v);
        Assert.Contains("{2}", v);
    }

    [Fact]
    public void Get_NetworkScanDone_ContainsPlaceholders()
    {
        var v = Create("en").Get("net.scan_done");
        Assert.Contains("{0}", v);
        Assert.Contains("{1}", v);
    }

    [Fact]
    public void Get_TabNetwork_NonEmpty_InBothLanguages()
    {
        Assert.NotEmpty(Create("en").Get("tab.network"));
        Assert.NotEmpty(Create("ne").Get("tab.network"));
    }

    // ── Disk-based path (Load + seed) ─────────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_SeedsFileFromEmbedded()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_LocTest_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "localization.json");
        try
        {
            using var svc = new LocalizationService(path);
            svc.Load();
            Assert.True(File.Exists(path), "localization.json should be created on first Load()");
            Assert.True(new FileInfo(path).Length > 0, "seeded file must not be empty");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Load_FileAbsent_ThenGetKey_ReturnsEnglishText()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_LocTest_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "localization.json");
        try
        {
            using var svc = new LocalizationService(path);
            svc.Load();
            Assert.Equal("Exit", svc.Get("app.exit"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Load_FilePresent_ReadsFromDisk()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_LocTest_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "localization.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, """{"custom.key":{"en":"hello","ne":"नमस्ते"}}""");
            using var svc = new LocalizationService(path);
            svc.Load();
            Assert.Equal("hello", svc.Get("custom.key"));
            // Missing embedded keys are merged in — disk values take precedence over embedded.
            Assert.Equal("Exit", svc.Get("app.exit"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void Load_EmptyPath_EmbeddedConstructor_IsNoOp()
    {
        // Calling Load() on the embedded-only (no-arg) constructor must not throw
        // or log warnings about missing paths — it is a documented no-op.
        using var svc = new LocalizationService();
        svc.Load(); // must not throw
        Assert.Equal("Exit", svc.Get("app.exit")); // data still intact
    }

    [Fact]
    public void Load_CorruptedDiskFile_DoesNotThrow_FallsBackToEmpty()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_LocTest_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "localization.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "NOT VALID JSON {{{{");
            using var svc = new LocalizationService(path);
            var ex = Record.Exception(() => svc.Load());
            Assert.Null(ex);
            // corrupted disk file causes LoadFromDisk to no-op; MergeMissingFromEmbedded
            // then fills all keys from embedded strings so the app remains usable.
            Assert.Equal("Exit", svc.Get("app.exit"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SetLanguage_EmptyString_DoesNotChangeLanguage()
    {
        var svc = Create("en");
        svc.SetLanguage("");
        Assert.Equal("en", svc.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_WhitespaceOnly_DoesNotChangeLanguage()
    {
        var svc = Create("ne");
        svc.SetLanguage("   ");
        Assert.Equal("ne", svc.CurrentLanguage);
    }
}

