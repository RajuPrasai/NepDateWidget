namespace NepDateWidget.Services;

/// <summary>
/// In-memory localization service using a two-level dictionary.
/// All strings for EN and NE are defined here in one place.
/// Keys are stable, descriptive, and dot-separated by section.
///
/// Adding a new language:
///   1. Add an entry to <see cref="_strings"/> below.
///   2. Call SetLanguage("xx") - everything updates automatically.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private string _language = "en";

    // ── String table ─────────────────────────────────────────────────────────
    // Format: key → { "en" → text, "ne" → text }

    private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        // ── General ──────────────────────────────────────────────────────────
        ["app.name"] = new() { ["en"] = "Nepali Calendar", ["ne"] = "नेपाली पात्रो" },
        ["app.exit"] = new() { ["en"] = "Exit", ["ne"] = "बन्द गर्नुहोस्" },
        ["app.settings"] = new() { ["en"] = "Settings", ["ne"] = "सेटिङ" },

        // ── Mini bar ─────────────────────────────────────────────────────────
        ["minibar.today"] = new() { ["en"] = "Today", ["ne"] = "आज" },
        ["minibar.expand_hint"] = new() { ["en"] = "Click to expand", ["ne"] = "खोल्न क्लिक गर्नुहोस्" },

        // ── Calendar holiday countdown ───────────────────────────────────────
        // Header banner placeholders: {0} = day count, {1} = holiday name.
        // For today/tomorrow templates {0} is the holiday name (no days).
        ["calendar.holiday.today"]    = new() { ["en"] = "Today: {0}",        ["ne"] = "आज: {0}" },
        ["calendar.holiday.tomorrow"] = new() { ["en"] = "Tomorrow: {0}",     ["ne"] = "भोलि: {0}" },
        ["calendar.holiday.in_days"]  = new() { ["en"] = "{0} days until {1}", ["ne"] = "{1} सम्म {0} दिन" },
        // Compact "+N more events" hint shown under the primary line when the
        // upcoming day carries multiple events. Clicking the banner opens a
        // popup with the full list.
        ["calendar.holiday.more_events"]      = new() { ["en"] = "+{0} more",            ["ne"] = "+{0} थप" },
        ["calendar.holiday.more_events_one"]  = new() { ["en"] = "+1 more",              ["ne"] = "+१ थप" },
        ["calendar.holiday.popup_title"]      = new() { ["en"] = "Upcoming holidays",    ["ne"] = "आगामी बिदाहरू" },
        ["calendar.holiday.popup_today"]      = new() { ["en"] = "Today",                ["ne"] = "आज" },
        ["calendar.holiday.popup_tomorrow"]   = new() { ["en"] = "Tomorrow",             ["ne"] = "भोलि" },
        ["calendar.holiday.popup_in_days"]    = new() { ["en"] = "in {0} days",          ["ne"] = "{0} दिनमा" },

        // ── Calendar copy-date context menu (right-click on any day) ─────────
        // The menu shows a non-clickable "Copy" header followed by four format
        // options whose visible text is the formatted date itself.
        ["calendar.copy.title"] = new() { ["en"] = "Copy", ["ne"] = "कपी गर्नुहोस्" },

        // ── Full day-of-week names ────────────────────────────────────────────
        ["dow.full.sun"] = new() { ["en"] = "Sunday", ["ne"] = "आइतबार" },
        ["dow.full.mon"] = new() { ["en"] = "Monday", ["ne"] = "सोमबार" },
        ["dow.full.tue"] = new() { ["en"] = "Tuesday", ["ne"] = "मंगलबार" },
        ["dow.full.wed"] = new() { ["en"] = "Wednesday", ["ne"] = "बुधबार" },
        ["dow.full.thu"] = new() { ["en"] = "Thursday", ["ne"] = "बिहिबार" },
        ["dow.full.fri"] = new() { ["en"] = "Friday", ["ne"] = "शुक्रबार" },
        ["dow.full.sat"] = new() { ["en"] = "Saturday", ["ne"] = "शनिबार" },

        // ── Calendar header ───────────────────────────────────────────────────
        ["calendar.prev_month"] = new() { ["en"] = "Previous month", ["ne"] = "अघिल्लो महिना" },
        ["calendar.next_month"] = new() { ["en"] = "Next month", ["ne"] = "अर्को महिना" },
        ["calendar.go_today"] = new() { ["en"] = "Go to today", ["ne"] = "आजको मिति" },

        // ── Day-of-week headers (short, 3-letter) ────────────────────────────────
        ["dow.sun"] = new() { ["en"] = "Sun", ["ne"] = "आइ" },
        ["dow.mon"] = new() { ["en"] = "Mon", ["ne"] = "सोम" },
        ["dow.tue"] = new() { ["en"] = "Tue", ["ne"] = "मंगल" },
        ["dow.wed"] = new() { ["en"] = "Wed", ["ne"] = "बुध" },
        ["dow.thu"] = new() { ["en"] = "Thu", ["ne"] = "बिहि" },
        ["dow.fri"] = new() { ["en"] = "Fri", ["ne"] = "शुक्र" },
        ["dow.sat"] = new() { ["en"] = "Sat", ["ne"] = "शनि" },

        // ── Converter ─────────────────────────────────────────────────────────
        ["converter.title"] = new() { ["en"] = "Date Converter", ["ne"] = "मिति रूपान्तर" },
        ["converter.ad_to_bs"] = new() { ["en"] = "AD → BS", ["ne"] = "ई.सं → वि.सं" },
        ["converter.bs_to_ad"] = new() { ["en"] = "BS → AD", ["ne"] = "वि.सं → ई.सं" },
        ["converter.switch_label"] = new() { ["en"] = "Switch",  ["ne"] = "स्विच" },
        ["converter.input_label"]  = new() { ["en"] = "Input",   ["ne"] = "इनपुट" },
        ["converter.convert_btn"] = new() { ["en"] = "Convert", ["ne"] = "रूपान्तरण" },
        ["converter.year_label"] = new() { ["en"] = "Year", ["ne"] = "वर्ष" },
        ["converter.month_label"] = new() { ["en"] = "Month", ["ne"] = "महिना" },
        ["converter.day_label"] = new() { ["en"] = "Day", ["ne"] = "दिन" },
        ["converter.result_label"] = new() { ["en"] = "Result", ["ne"] = "नतिजा" },
        ["converter.error_invalid"] = new() { ["en"] = "Invalid date", ["ne"] = "गलत मिति" },
        ["converter.input_hint"] = new() { ["en"] = "e.g. 2081/01/15", ["ne"] = "जस्तै: २०८१/०१/१५" },

        // ── Context menu ─────────────────────────────────────────────────────
        ["menu.always_on_top"] = new() { ["en"] = "Always on top", ["ne"] = "सधैँ माथि" },
        ["menu.auto_start"] = new() { ["en"] = "Start with Windows", ["ne"] = "Windows सँगै सुरु हुने" },
        ["menu.show_clock"] = new() { ["en"] = "Show clock", ["ne"] = "घडी देखाउने" },
        ["menu.show_timezone"] = new() { ["en"] = "Show timezone", ["ne"] = "टाइमजोन देखाउने" },
        ["settings.collapsed_display"] = new() { ["en"] = "Collapsed Display", ["ne"] = "सानो दृश्य" },
        ["menu.animation"] = new() { ["en"] = "Animation", ["ne"] = "एनिमेसन" },
        ["menu.language"] = new() { ["en"] = "Language", ["ne"] = "भाषा" },
        ["menu.theme"] = new() { ["en"] = "Theme", ["ne"] = "थिम" },
        ["menu.theme_dark"] = new() { ["en"] = "Dark", ["ne"] = "अँध्यारो" },
        ["menu.theme_light"] = new() { ["en"] = "Light", ["ne"] = "उज्यालो" },
        ["menu.background"] = new() { ["en"] = "Background", ["ne"] = "ब्याकग्राउन्ड" },
        ["menu.corner_style"] = new() { ["en"] = "Corner style", ["ne"] = "कुना शैली" },
        ["menu.corner_rounded"] = new() { ["en"] = "Rounded", ["ne"] = "गोलो" },
        ["menu.corner_sharp"] = new() { ["en"] = "Sharp", ["ne"] = "तीखो" },
        ["menu.preset_default"] = new() { ["en"] = "Default", ["ne"] = "डिफल्ट" },
        ["menu.preset_ocean"] = new() { ["en"] = "Ocean", ["ne"] = "समुद्र" },
        ["menu.preset_forest"] = new() { ["en"] = "Forest", ["ne"] = "वन" },
        ["menu.preset_sunset"] = new() { ["en"] = "Sunset", ["ne"] = "सूर्यास्त" },
        ["menu.preset_monochrome"] = new() { ["en"] = "Monochrome", ["ne"] = "एकरंगी" },
        ["menu.preset_aurora"] = new() { ["en"] = "Aurora", ["ne"] = "औरोरा" },
        ["menu.preset_cherry"] = new() { ["en"] = "Cherry", ["ne"] = "चेरी" },
        ["menu.preset_midnight"] = new() { ["en"] = "Midnight", ["ne"] = "मध्यरात" },
        ["menu.preset_slate"] = new() { ["en"] = "Slate", ["ne"] = "स्लेट" },
        ["menu.preset_ember"] = new() { ["en"] = "Ember", ["ne"] = "अंगारो" },
        ["menu.exit"] = new() { ["en"] = "Exit", ["ne"] = "बन्द गर्नुहोस्" },
        ["menu.copy_today"] = new() { ["en"] = "Copy", ["ne"] = "कपी" },
        ["menu.tools_section"]   = new() { ["en"] = "Date",    ["ne"] = "मिति" },
        ["menu.tools_convert"]   = new() { ["en"] = "Convert", ["ne"] = "रूपान्तर" },
        ["menu.tools_days"]      = new() { ["en"] = "Days",    ["ne"] = "दिन" },
        ["menu.tools_time"]      = new() { ["en"] = "Time",    ["ne"] = "समय" },
        ["menu.banking_section"] = new() { ["en"] = "Bank",    ["ne"] = "बैंक" },
        ["menu.banking_interest"] = new() { ["en"] = "Interest",      ["ne"] = "ब्याज" },
        ["menu.banking_emi"]      = new() { ["en"] = "EMI Calculator", ["ne"] = "किस्ता गणना" },

        // ── Interest calculator ───────────────────────────────────────────────
        ["interest.title"]           = new() { ["en"] = "Interest Calculator",           ["ne"] = "ब्याज गणना" },
        ["interest.section"]         = new() { ["en"] = "Interest",                      ["ne"] = "ब्याज" },
        ["interest.principal"]       = new() { ["en"] = "Principal",                     ["ne"] = "मूलधन" },
        ["interest.from"]            = new() { ["en"] = "From",                          ["ne"] = "बाट" },
        ["interest.to"]              = new() { ["en"] = "To",                            ["ne"] = "सम्म" },
        ["interest.rate_col"]        = new() { ["en"] = "Rate % p.a.",                   ["ne"] = "दर % प्रतिवर्ष" },
        ["interest.add_period"]      = new() { ["en"] = "+ Add Period",                  ["ne"] = "+ अवधि थप्नुहोस्" },
        ["interest.calculate"]       = new() { ["en"] = "Calculate",                     ["ne"] = "हिसाब गर्नुहोस्" },
        ["interest.error_principal"] = new() { ["en"] = "Enter a valid principal amount",["ne"] = "सही रकम हाल्नुहोस्" },
        ["interest.error_from"]      = new() { ["en"] = "Enter a valid From date",       ["ne"] = "सही सुरु मिति हाल्नुहोस्" },
        ["interest.error_to"]        = new() { ["en"] = "Enter a valid To date",         ["ne"] = "सही अन्त्य मिति हाल्नुहोस्" },
        ["interest.error_row_date"]  = new() { ["en"] = "Invalid date in row",           ["ne"] = "यो पंक्तिमा सही मिति हाल्नुहोस्" },
        ["interest.error_row_rate"]  = new() { ["en"] = "Enter a valid rate in row",     ["ne"] = "यो पंक्तिमा सही दर हाल्नुहोस्" },
        ["interest.error_negative_days"] = new() { ["en"] = "To date must be after From date", ["ne"] = "अन्त्य मिति सुरु भन्दा पछि हुनुपर्छ" },

        // ── Tabs ──────────────────────────────────────────────────────────────
        ["tab.calendar"] = new() { ["en"] = "Home",     ["ne"] = "होम" },
        ["tab.converter"] = new() { ["en"] = "Date",     ["ne"] = "मिति" },
        ["tab.settings"] = new() { ["en"] = "Settings", ["ne"] = "सेटिङ" },

        // ── Tools tab ─────────────────────────────────────────────────────────
        ["tools.tab"] = new() { ["en"] = "Tools", ["ne"] = "टुल्स" },
        ["tools.mode_convert"] = new() { ["en"] = "Convert", ["ne"] = "रूपान्तर" },
        ["tools.mode_days"] = new() { ["en"] = "Days", ["ne"] = "दिन" },
        ["tools.mode_age"] = new() { ["en"] = "Age", ["ne"] = "उमेर" },
        ["tools.days_addsub"] = new() { ["en"] = "Add/Sub", ["ne"] = "जोड/घटाउ" },
        ["tools.days_diff"] = new() { ["en"] = "Diff", ["ne"] = "अन्तर" },
        ["tools.mode_time"] = new() { ["en"] = "Time", ["ne"] = "समय" },
        ["tools.time_from"] = new() { ["en"] = "From", ["ne"] = "बाट" },
        ["tools.time_to"] = new() { ["en"] = "To", ["ne"] = "मा" },
        ["tools.age_from"] = new() { ["en"] = "From", ["ne"] = "देखि" },
        ["tools.age_to"] = new() { ["en"] = "To", ["ne"] = "सम्म" },

        // ── Fiscal year (calendar footer) ─────────────────────────────────────
        ["fiscal.label"] = new() { ["en"] = "FY", ["ne"] = "आ.व." },
        ["fiscal.quarter"] = new() { ["en"] = "Q", ["ne"] = "त्रै" },
        ["fiscal.days_to_qend"] = new() { ["en"] = "days left", ["ne"] = "दिन बाँकी" },
        ["fiscal.days_to_yend"] = new() { ["en"] = "days to year end", ["ne"] = "दिनमा वर्षको अन्त्य" },

        // ── Settings ──────────────────────────────────────────────────────────
        ["settings.appearance"] = new() { ["en"] = "Appearance", ["ne"] = "रूप" },
        ["settings.behavior"] = new() { ["en"] = "Behavior", ["ne"] = "व्यवहार" },
        ["settings.reset"] = new() { ["en"] = "Reset to defaults", ["ne"] = "सबै डिफल्ट बनाउनुहोस्" },
        ["settings.language"] = new() { ["en"] = "Language", ["ne"] = "भाषा" },
        ["settings.theme"] = new() { ["en"] = "Theme", ["ne"] = "थिम" },
        ["settings.background"] = new() { ["en"] = "Background", ["ne"] = "ब्याकग्राउन्ड" },
        ["settings.corner_style"] = new() { ["en"] = "Corner style", ["ne"] = "कुना शैली" },
        ["settings.font"] = new() { ["en"] = "Font", ["ne"] = "फन्ट" },
        ["settings.always_on_top"] = new() { ["en"] = "Always on top", ["ne"] = "सधैँ माथि" },
        ["settings.transparent_collapsed"] = new() { ["en"] = "Transparent Background", ["ne"] = "पारदर्शी ब्याकग्राउन्ड" },
        ["settings.show_timezone"] = new() { ["en"] = "Show timezone", ["ne"] = "टाइमजोन देखाउने" },
        ["settings.timezone"] = new() { ["en"] = "Timezone", ["ne"] = "टाइमजोन" },
        ["settings.show_offset"] = new() { ["en"] = "Show offset", ["ne"] = "अफसेट देखाउने" },
        ["settings.show_day"] = new() { ["en"] = "Show day of week", ["ne"] = "बार देखाउने" },
        ["settings.show_english"] = new() { ["en"] = "Show English date", ["ne"] = "अंग्रेजी मिति देखाउने" },
        ["settings.show_holiday_countdown"] = new() { ["en"] = "Show holiday countdown", ["ne"] = "बिदा गणना देखाउने" },
        ["settings.show_daily_events_notification"] = new() { ["en"] = "Notify me about today's events", ["ne"] = "आजका कार्यक्रम सूचित गर" },
        ["settings.animation"] = new() { ["en"] = "Animation", ["ne"] = "एनिमेसन" },
        ["settings.auto_start"] = new() { ["en"] = "Start with Windows", ["ne"] = "Windows सँगै सुरु हुने" },
        ["settings.apply"] = new() { ["en"] = "Apply", ["ne"] = "लागू गर्नुहोस्" },

        // ── Calendar settings ─────────────────────────────────────────────
        ["settings.calendar"] = new() { ["en"] = "Calendar", ["ne"] = "पात्रो" },
        ["settings.show_eng_day_nums"] = new() { ["en"] = "Show English day", ["ne"] = "अंग्रेजी दिन देखाउने" },
        ["settings.highlight_saturdays"] = new() { ["en"] = "Highlight Saturdays", ["ne"] = "शनिबार हाइलाइट गर्ने" },
        ["settings.highlight_sundays"] = new() { ["en"] = "Highlight Sundays", ["ne"] = "आइतबार हाइलाइट गर्ने" },
        ["settings.highlight_color"] = new() { ["en"] = "Highlight Color", ["ne"] = "हाइलाइट रंग" },
        ["settings.clock_format"] = new() { ["en"] = "Clock format", ["ne"] = "घडी ढाँचा" },
        ["settings.log_size"] = new() { ["en"] = "Log file size", ["ne"] = "लग फाइल साइज" },
        ["settings.auto_collapse"] = new() { ["en"] = "Auto-collapse on focus loss", ["ne"] = "अरू थिच्दा आफै बन्द होस्" },
        ["settings.reset_defaults"] = new() { ["en"] = "Reset to defaults", ["ne"] = "सबै डिफल्ट बनाउनुहोस्" },
        ["settings.show_tithi"] = new() { ["en"] = "Show Tithi", ["ne"] = "तिथि देखाउने" },
        ["settings.show_events"] = new() { ["en"] = "Show events", ["ne"] = "चाडपर्व देखाउने" },
        ["settings.highlight_holidays"] = new() { ["en"] = "Highlight public holidays", ["ne"] = "सार्वजनिक बिदा हाइलाइट गर्ने" },
        ["settings.show_fiscal_year"] = new() { ["en"] = "Show fiscal year", ["ne"] = "आर्थिक वर्ष देखाउने" },
        ["settings.notification_duration"] = new() { ["en"] = "Notification duration", ["ne"] = "सूचना अवधि" },
        ["settings.notification_sound"] = new() { ["en"] = "Notification sound", ["ne"] = "सूचना ध्वनि" },
        ["settings.show_seconds"] = new() { ["en"] = "Show seconds in clock", ["ne"] = "सेकेन्ड देखाउने" },
        ["settings.hide_fullscreen"] = new() { ["en"] = "Hide on fullscreen apps", ["ne"] = "फुलस्क्रिनमा लुकाउने" },
        ["settings.reminder_interval"] = new() { ["en"] = "Reminder check interval", ["ne"] = "रिमाइन्डर जाँच अन्तराल" },
        ["settings.notification"] = new() { ["en"] = "Notification", ["ne"] = "सूचना" },

        // ── Updates ─────────────────────────────────────────────────────────
        ["settings.updates"]            = new() { ["en"] = "Updates",                ["ne"] = "अपडेट" },
        ["settings.auto_update"]        = new() { ["en"] = "Check for updates automatically", ["ne"] = "स्वतः अपडेट जाँच गर्ने" },
        ["settings.check_update_now"]   = new() { ["en"] = "Check for updates now",  ["ne"] = "अहिले अपडेट जाँच्नुहोस्" },
        ["settings.update_checking"]    = new() { ["en"] = "Checking for updates…",  ["ne"] = "अपडेट जाँच गर्दै…" },
        ["settings.update_uptodate"]    = new() { ["en"] = "You're up to date (v{0}).", ["ne"] = "तपाईँ नवीनतम संस्करण v{0} मा हुनुहुन्छ।" },
        ["settings.update_downloading"] = new() { ["en"] = "Downloading v{0}…",      ["ne"] = "v{0} डाउनलोड गर्दै…" },
        ["settings.update_failed"]      = new() { ["en"] = "Update failed.",         ["ne"] = "अपडेट असफल।" },
        ["settings.update_unavailable"] = new() { ["en"] = "Updates are not available in this build.", ["ne"] = "यो बिल्डमा अपडेट उपलब्ध छैन।" },

        // ── Day Info popup ────────────────────────────────────────────────────
        ["dayinfo.holiday_badge"] = new() { ["en"] = "Public Holiday", ["ne"] = "सार्वजनिक बिदा" },
        ["dayinfo.tithi_label"] = new() { ["en"] = "Tithi", ["ne"] = "तिथि" },
        ["dayinfo.events_label"] = new() { ["en"] = "Events", ["ne"] = "चाडपर्व" },
        ["dayinfo.no_events"] = new() { ["en"] = "No events today", ["ne"] = "आज केही चाडपर्व छैन" },
        ["dayinfo.note_label"] = new() { ["en"] = "Note", ["ne"] = "नोट" },
        ["dayinfo.add_note"] = new() { ["en"] = "Add Note", ["ne"] = "नोट थप्नुहोस्" },
        ["dayinfo.edit_note"] = new() { ["en"] = "Edit Note", ["ne"] = "नोट एडिट" },
        ["dayinfo.save_note"] = new() { ["en"] = "Save", ["ne"] = "सेभ गर्नुहोस्" },
        ["dayinfo.cancel"] = new() { ["en"] = "Cancel", ["ne"] = "रद्द" },
        ["dayinfo.add_reminder"] = new() { ["en"] = "Add Reminder", ["ne"] = "रिमाइन्डर थप्नुहोस्" },
        ["dayinfo.reminders_label"] = new() { ["en"] = "Reminders", ["ne"] = "रिमाइन्डरहरू" },
        ["dayinfo.no_reminders"] = new() { ["en"] = "No reminders", ["ne"] = "रिमाइन्डर छैन" },
        ["dayinfo.no_note"] = new() { ["en"] = "No note for this day", ["ne"] = "यो दिनको नोट छैन" },

        // ── Unit tab ────────────────────────────────────────────────────
        ["tab.unit"]             = new() { ["en"] = "Unit",               ["ne"] = "एकाइ" },
        ["unit.mode_area"]             = new() { ["en"] = "Area",                     ["ne"] = "क्षेत्र" },
        ["unit.mode_script"]           = new() { ["en"] = "Script",                   ["ne"] = "लिपि" },
        ["unit.mode_weight"]           = new() { ["en"] = "Weight",                   ["ne"] = "तौल" },
        ["unit.area.from"]             = new() { ["en"] = "From",                     ["ne"] = "बाट" },
        ["unit.area.to"]               = new() { ["en"] = "To",                       ["ne"] = "मा" },
        ["unit.weight.from"]           = new() { ["en"] = "From",                     ["ne"] = "बाट" },
        ["unit.weight.to"]             = new() { ["en"] = "To",                       ["ne"] = "मा" },
        ["unit.copy"]                  = new() { ["en"] = "Copy",                     ["ne"] = "कपी" },
        ["unit.script.roman_in"]       = new() { ["en"] = "Roman input",              ["ne"] = "रोमन इनपुट" },
        ["unit.script.deva_out"]       = new() { ["en"] = "Devanagari output",        ["ne"] = "देवनागरी आउटपुट" },
        ["unit.script.deva_in"]        = new() { ["en"] = "Devanagari input",         ["ne"] = "देवनागरी इनपुट" },
        ["unit.script.roman_out"]      = new() { ["en"] = "Roman output",             ["ne"] = "रोमन आउटपुट" },
        ["unit.script.hint"]           = new() { ["en"] = "Use 'aa' for ā, 'ii' for ī", ["ne"] = "दीर्घ स्वरका लागि 'aa', 'ii' प्रयोग गर्नुहोस्" },
        ["texttools.script_input"]     = new() { ["en"] = "Input",                    ["ne"] = "इनपुट" },
        ["texttools.script_output"]    = new() { ["en"] = "Output",                   ["ne"] = "आउटपुट" },
        ["texttools.script_r2d"]       = new() { ["en"] = "Roman \u2192 Devnagari",   ["ne"] = "Roman \u2192 देवनागरी" },
        ["texttools.script_d2r"]       = new() { ["en"] = "Devnagari \u2192 Roman",   ["ne"] = "देवनागरी \u2192 Roman" },
        ["unit.error_invalid_number"]  = new() { ["en"] = "Enter a valid number",     ["ne"] = "सही नम्बर हाल्नुहोस्" },
        ["unit.error_negative"]        = new() { ["en"] = "Value must be 0 or more",  ["ne"] = "० वा बढी हुनुपर्छ" },
        ["menu.unit_section"]          = new() { ["en"] = "Unit",              ["ne"] = "एकाइ" },
        ["menu.network_section"]       = new() { ["en"] = "Network",             ["ne"] = "नेटवर्क" },
        ["menu.unit_area"]             = new() { ["en"] = "Area",                     ["ne"] = "क्षेत्र" },
        ["menu.unit_script"]           = new() { ["en"] = "Script",                   ["ne"] = "लिपि" },
        ["menu.text_script"]           = new() { ["en"] = "Script",                   ["ne"] = "लिपि" },
        ["menu.unit_weight"]           = new() { ["en"] = "Weight",                   ["ne"] = "तौल" },

        // ── Banking tab ───────────────────────────────────────────────────────
        ["tab.banking"]              = new() { ["en"] = "Bank",                         ["ne"] = "बैंक" },
        ["banking.mode_interest"]    = new() { ["en"] = "Interest",                      ["ne"] = "ब्याज" },
        ["banking.mode_emi"]         = new() { ["en"] = "EMI",                           ["ne"] = "किस्ता" },
        ["banking.emi_loan"]         = new() { ["en"] = "Loan Amount",                   ["ne"] = "ऋण रकम" },
        ["banking.emi_rate"]         = new() { ["en"] = "Annual Rate (%)",               ["ne"] = "वार्षिक दर (%)" },
        ["banking.emi_months"]       = new() { ["en"] = "Months",                        ["ne"] = "महिना" },
        ["banking.emi_calculate"]    = new() { ["en"] = "Calculate EMI",                 ["ne"] = "किस्ता गणना" },
        ["banking.emi_monthly"]      = new() { ["en"] = "Monthly EMI",                   ["ne"] = "मासिक किस्ता" },
        ["banking.emi_total_payment"] = new() { ["en"] = "Total Payment",                ["ne"] = "कुल भुक्तानी" },
        ["banking.emi_total_interest"] = new() { ["en"] = "Total Interest",              ["ne"] = "कुल ब्याज" },
        ["banking.emi_error_loan"]   = new() { ["en"] = "Enter a valid loan amount",     ["ne"] = "सही ऋण रकम हाल्नुहोस्" },
        ["banking.emi_error_rate"]   = new() { ["en"] = "Enter a valid annual rate",     ["ne"] = "सही वार्षिक दर हाल्नुहोस्" },
        ["banking.emi_error_months"] = new() { ["en"] = "Enter a valid number of months", ["ne"] = "सही महिना संख्या हाल्नुहोस्" },
        ["banking.emi_start_date_bs"]  = new() { ["en"] = "Start Date (BS)",               ["ne"] = "सुरु मिति (बि.सं.)" },
        ["banking.emi_error_start_date"] = new() { ["en"] = "Enter start date as YYYY/MM/DD (BS)", ["ne"] = "सुरु मिति YYYY/MM/DD (बि.सं.) मा हाल्नुहोस्" },
        ["banking.emi_col_year"]     = new() { ["en"] = "Period",                        ["ne"] = "अवधि" },
        ["banking.emi_col_principal"] = new() { ["en"] = "Principal",                    ["ne"] = "मूलधन" },
        ["banking.emi_col_interest"] = new() { ["en"] = "Interest",                      ["ne"] = "ब्याज" },
        ["banking.emi_col_total"]    = new() { ["en"] = "Total",                         ["ne"] = "जम्मा" },
        ["banking.emi_col_balance"]  = new() { ["en"] = "Balance",                       ["ne"] = "बाँकी" },

        // ── Network Tools tab ─────────────────────────────────────────────────
        ["tab.network"]           = new() { ["en"] = "Network",                       ["ne"] = "नेटवर्क" },
        ["net.mode_myip"]         = new() { ["en"] = "My IP",                         ["ne"] = "मेरो IP" },
        ["net.mode_ping"]         = new() { ["en"] = "Ping",                          ["ne"] = "पिङ" },
        ["net.mode_scan"]         = new() { ["en"] = "Scan",                         ["ne"] = "स्क्यान" },
        ["net.mode_trace"]        = new() { ["en"] = "Trace",                        ["ne"] = "ट्रेस" },
        ["net.mode_whois"]        = new() { ["en"] = "Whois",                         ["ne"] = "व्होइज" },
        ["net.mode_dns"]          = new() { ["en"] = "DNS",                           ["ne"] = "DNS" },
        ["net.fetch"]             = new() { ["en"] = "Fetch",                         ["ne"] = "ल्याउनुहोस्" },
        ["net.ping"]              = new() { ["en"] = "Ping",                          ["ne"] = "पिङ" },
        ["net.scan"]              = new() { ["en"] = "Scan",                          ["ne"] = "स्क्यान" },
        ["net.trace"]             = new() { ["en"] = "Trace",                         ["ne"] = "ट्रेस" },
        ["net.whois"]             = new() { ["en"] = "Lookup",                        ["ne"] = "खोज्नुहोस्" },
        ["net.dns"]               = new() { ["en"] = "Lookup",                        ["ne"] = "खोज्नुहोस्" },
        ["net.copy"]              = new() { ["en"] = "Copy",                          ["ne"] = "कपी" },
        ["net.host"]              = new() { ["en"] = "Host / IP",                     ["ne"] = "होस्ट / IP" },
        ["net.count"]             = new() { ["en"] = "Count",                         ["ne"] = "संख्या" },
        ["net.domain"]            = new() { ["en"] = "Domain",                        ["ne"] = "डोमेन" },
        ["net.loading"]           = new() { ["en"] = "Working…",                      ["ne"] = "गर्दैछ…" },
        ["net.offline"]           = new() { ["en"] = "No internet connection",        ["ne"] = "इन्टरनेट छैन" },
        ["net.error"]             = new() { ["en"] = "An error occurred",             ["ne"] = "केही गडबड भयो" },
        ["net.no_result"]         = new() { ["en"] = "No results returned",           ["ne"] = "केही भेटिएन" },
        ["net.dns_not_found"]     = new() { ["en"] = "Host not found",                ["ne"] = "होस्ट भेटिएन" },
        ["net.no_network"]        = new() { ["en"] = "No active network interface",   ["ne"] = "नेटवर्क जडान छैन" },
        ["net.scanning"]          = new() { ["en"] = "Scanning…",                     ["ne"] = "स्क्यान हुँदैछ…" },
        ["net.scan_progress"]     = new() { ["en"] = "Scanned {0}/{1} - {2} found",   ["ne"] = "स्क्यान {0}/{1} - {2} भेटियो" },
        ["net.scan_done"]         = new() { ["en"] = "Done - {0} of {1} hosts online", ["ne"] = "सकियो - {1} मध्ये {0} होस्ट अनलाइन" },
        ["net.col_ip"]            = new() { ["en"] = "IP Address",                    ["ne"] = "IP ठेगाना" },
        ["net.col_host"]          = new() { ["en"] = "Host Name",                     ["ne"] = "होस्ट नाम" },
        ["net.col_mac"]           = new() { ["en"] = "MAC Address",                   ["ne"] = "MAC ठेगाना" },
        ["net.col_mfr"]           = new() { ["en"] = "Manufacturer",                  ["ne"] = "निर्माता" },
        ["net.col_type"]          = new() { ["en"] = "Device Type",                   ["ne"] = "उपकरण प्रकार" },
        ["net.col_status"]        = new() { ["en"] = "Status",                        ["ne"] = "स्थिति" },
        ["net.col_rtt"]           = new() { ["en"] = "RTT (ms)",                      ["ne"] = "RTT (ms)" },

        // ── Text Tools tab ────────────────────────────────────────────────────
        ["tab.text"]                   = new() { ["en"] = "Text",                        ["ne"] = "टेक्स्ट" },
        ["texttools.mode_unicode"]     = new() { ["en"] = "Unicode",                     ["ne"] = "युनिकोड" },
        ["texttools.mode_word"]        = new() { ["en"] = "Word",                        ["ne"] = "शब्द" },
        ["texttools.mode_password"]    = new() { ["en"] = "Password",                    ["ne"] = "पासवर्ड" },
        ["texttools.mode_script"]      = new() { ["en"] = "Script",                      ["ne"] = "लिपि" },
        ["texttools.unicode_input"]    = new() { ["en"] = "Input",                       ["ne"] = "इनपुट" },
        ["texttools.unicode_output"]   = new() { ["en"] = "Output",                      ["ne"] = "आउटपुट" },
        ["texttools.preeti_to_unicode"]= new() { ["en"] = "Preeti → Unicode",            ["ne"] = "प्रिती → युनिकोड" },
        ["texttools.unicode_to_preeti"]= new() { ["en"] = "Unicode → Preeti",            ["ne"] = "युनिकोड → प्रिती" },
        ["texttools.copy"]             = new() { ["en"] = "Copy",                        ["ne"] = "कपी" },
        ["texttools.word_output"]      = new() { ["en"] = "Output",                      ["ne"] = "आउटपुट" },
        ["texttools.word_input"]       = new() { ["en"] = "Text",                        ["ne"] = "पाठ" },
        ["texttools.word_count"]       = new() { ["en"] = "Words",                       ["ne"] = "शब्द" },
        ["texttools.char_count"]       = new() { ["en"] = "Chars",                       ["ne"] = "अक्षर" },
        ["texttools.char_no_spaces"]   = new() { ["en"] = "No Spaces",                   ["ne"] = "स्पेसबिना" },
        ["texttools.case_upper"]       = new() { ["en"] = "UPPER",                       ["ne"] = "ठूलो" },
        ["texttools.case_lower"]       = new() { ["en"] = "lower",                       ["ne"] = "सानो" },
        ["texttools.case_title"]       = new() { ["en"] = "Title Case",                  ["ne"] = "शीर्षक" },
        ["texttools.case_sentence"]    = new() { ["en"] = "Sentence case",               ["ne"] = "वाक्य" },
        ["texttools.case_snake"]       = new() { ["en"] = "snake_case",                  ["ne"] = "स्नेक" },
        ["texttools.case_camel"]       = new() { ["en"] = "camelCase",                   ["ne"] = "क्यामल" },
        ["texttools.pw_length"]        = new() { ["en"] = "Length",                      ["ne"] = "लम्बाइ" },
        ["texttools.pw_upper"]         = new() { ["en"] = "A-Z",                         ["ne"] = "A-Z" },
        ["texttools.pw_lower"]         = new() { ["en"] = "a-z",                         ["ne"] = "a-z" },
        ["texttools.pw_numbers"]       = new() { ["en"] = "0-9",                         ["ne"] = "0-9" },
        ["texttools.pw_symbols"]       = new() { ["en"] = "Symbols",                     ["ne"] = "चिन्ह" },
        ["texttools.pw_nepali"]        = new() { ["en"] = "Nepali",                      ["ne"] = "नेपाली" },
        ["texttools.pw_generate"]      = new() { ["en"] = "Generate",                    ["ne"] = "बनाउनुहोस्" },
        ["texttools.pw_strength"]      = new() { ["en"] = "Strength",                    ["ne"] = "बल" },
        ["texttools.pw_check"]         = new() { ["en"] = "Check password strength",     ["ne"] = "पासवर्ड बल जाँच्नुहोस्" },
        ["texttools.pw_weak"]          = new() { ["en"] = "Weak",                        ["ne"] = "कमजोर" },
        ["texttools.pw_medium"]        = new() { ["en"] = "Medium",                      ["ne"] = "मध्यम" },
        ["texttools.pw_strong"]        = new() { ["en"] = "Strong",                      ["ne"] = "बलियो" },
        ["texttools.criteria_length"]  = new() { ["en"] = "8+ characters",               ["ne"] = "८+ अक्षर" },
        ["texttools.criteria_upper"]   = new() { ["en"] = "Uppercase letter",            ["ne"] = "ठूलो अक्षर" },
        ["texttools.criteria_lower"]   = new() { ["en"] = "Lowercase letter",            ["ne"] = "सानो अक्षर" },
        ["texttools.criteria_number"]  = new() { ["en"] = "Number",                      ["ne"] = "संख्या" },
        ["texttools.criteria_symbol"]  = new() { ["en"] = "Symbol",                      ["ne"] = "चिन्ह" },
        ["texttools.file_section"]     = new() { ["en"] = "Convert a File",               ["ne"] = "फाइल बदल्नुहोस्" },
        ["texttools.file_no_file"]     = new() { ["en"] = "No file selected",             ["ne"] = "फाइल छानिएको छैन" },
        ["texttools.file_browse"]      = new() { ["en"] = "Browse",                       ["ne"] = "छान्नुहोस्" },
        ["texttools.file_convert"]     = new() { ["en"] = "Convert & Save",               ["ne"] = "बदलेर सेभ गर्नुहोस्" },
        ["texttools.file_converting"]  = new() { ["en"] = "Converting...",                ["ne"] = "बदल्दैछ..." },
        ["texttools.file_saved"]       = new() { ["en"] = "Saved:",                       ["ne"] = "सेभ भयो:" },
        ["menu.text_section"]          = new() { ["en"] = "Text",                        ["ne"] = "पाठ" },
        ["menu.text_unicode"]          = new() { ["en"] = "Unicode",                     ["ne"] = "युनिकोड" },
        ["menu.text_word"]             = new() { ["en"] = "Word Tools",                  ["ne"] = "शब्द उपकरण" },
        ["menu.text_password"]         = new() { ["en"] = "Password",                    ["ne"] = "पासवर्ड" },

        // ── Reminders ─────────────────────────────────────────────────────────
        ["reminder.popup_title"]       = new() { ["en"] = "Reminders",                   ["ne"] = "रिमाइन्डरहरू" },
        ["reminder.add"]               = new() { ["en"] = "Add Reminder",                ["ne"] = "रिमाइन्डर थप्नुहोस्" },
        ["reminder.edit"]              = new() { ["en"] = "Edit Reminder",               ["ne"] = "रिमाइन्डर एडिट" },
        ["reminder.title"]             = new() { ["en"] = "Title",                       ["ne"] = "शीर्षक" },
        ["reminder.time"]              = new() { ["en"] = "Time",                        ["ne"] = "समय" },
        ["reminder.date"]              = new() { ["en"] = "Date",                        ["ne"] = "मिति" },
        ["reminder.notes"]             = new() { ["en"] = "Notes",                       ["ne"] = "नोट" },
        ["reminder.recurrence"]        = new() { ["en"] = "Repeat",                      ["ne"] = "दोहोर्‍याउने" },
        ["reminder.recurrence_none"]   = new() { ["en"] = "None",                        ["ne"] = "नदोहोर्‍याउने" },
        ["reminder.recurrence_daily"]  = new() { ["en"] = "Daily",                       ["ne"] = "दैनिक" },
        ["reminder.recurrence_weekly"] = new() { ["en"] = "Weekly",                      ["ne"] = "साप्ताहिक" },
        ["reminder.recurrence_monthly"]= new() { ["en"] = "Monthly",                     ["ne"] = "मासिक" },
        ["reminder.end_date"]          = new() { ["en"] = "End Date",                    ["ne"] = "अन्तिम मिति" },
        ["reminder.save"]              = new() { ["en"] = "Save",                        ["ne"] = "सेभ गर्नुहोस्" },
        ["reminder.cancel"]            = new() { ["en"] = "Cancel",                      ["ne"] = "रद्द गर्नुहोस्" },
        ["reminder.delete"]            = new() { ["en"] = "Delete",                      ["ne"] = "मेटाउनुहोस्" },
        ["reminder.no_reminders"]      = new() { ["en"] = "No reminders for this day",   ["ne"] = "यो दिनको रिमाइन्डर छैन" },
        ["reminder.title_required"]    = new() { ["en"] = "Title is required",           ["ne"] = "शीर्षक चाहिन्छ" },
        ["reminder.date_invalid"]      = new() { ["en"] = "Invalid date",               ["ne"] = "गलत मिति" },
        ["reminder.date_past"]         = new() { ["en"] = "Date is in the past",        ["ne"] = "मिति बितिसकेको छ" },
        ["reminder.time_invalid"]      = new() { ["en"] = "Invalid time (H:MM)",        ["ne"] = "गलत समय (H:MM)" },
        ["reminder.end_date_invalid"]  = new() { ["en"] = "Invalid end date",           ["ne"] = "गलत अन्तिम मिति" },
        ["reminder.end_date_before_start"] = new() { ["en"] = "End date before start",  ["ne"] = "अन्तिम मिति सुरु भन्दा अगाडि छ" },
        ["reminder.discard_title"]     = new() { ["en"] = "Discard changes?",           ["ne"] = "बदलेको नराख्ने?" },
        ["reminder.discard_yes"]       = new() { ["en"] = "Discard",                    ["ne"] = "हटाउ" },
        ["reminder.notification"]      = new() { ["en"] = "Reminder",                    ["ne"] = "रिमाइन्डर" },
        ["reminder.dismiss"]           = new() { ["en"] = "Dismiss",                     ["ne"] = "हटाउ" },

        // ── Daily events notification (first launch of each AD day) ──────────
        ["daily_events.header"] = new() { ["en"] = "Today's events", ["ne"] = "आजका कार्यक्रम" },
        ["daily_events.title"]  = new() { ["en"] = "Events today",   ["ne"] = "आजको दिनमा" },
        ["reminder.missed_badge"]      = new() { ["en"] = "Calendar ({0})",              ["ne"] = "पात्रो ({0})" },
        ["reminder.completed"]         = new() { ["en"] = "Completed",                   ["ne"] = "सकियो" },


        // ── RunBox ─────────────────────────────────────────────────────────────
        ["runbox.placeholder"]         = new() { ["en"] = "Search, open file, or run...", ["ne"] = "खोज्नुहोस्, फाइल खोल्नुहोस्..." },
        ["runbox.error"]               = new() { ["en"] = "Could not open",              ["ne"] = "खोल्न सकिएन" },

        // ── Settings: Hotkey ───────────────────────────────────────────────────
        ["settings.hotkey"]            = new() { ["en"] = "RunBox hotkey",               ["ne"] = "रनबक्स हटकी" },
        ["settings.hotkey_record"]     = new() { ["en"] = "Press keys...",               ["ne"] = "कुञ्जी थिच्नुहोस्..." },
        ["settings.hotkey_reserved"]   = new() { ["en"] = "Reserved shortcut",           ["ne"] = "आरक्षित सर्टकट" },
        ["settings.hotkey_clear"]      = new() { ["en"] = "Clear",                       ["ne"] = "खाली गर्नुहोस्" },

        // ── About tab ─────────────────────────────────────────────────────────
        ["tab.about"]                  = new() { ["en"] = "About",                       ["ne"] = "बारेमा" },
        ["about.version_label"]        = new() { ["en"] = "Version",                     ["ne"] = "भर्सन" },
        ["about.tagline"]              = new() { ["en"] = "A Nepali calendar & tools widget for your desktop.",   ["ne"] = "तपाईंको डेस्कटपको लागि नेपाली पात्रो र टुल्स।" },
        ["about.features_heading"]     = new() { ["en"] = "What's inside",               ["ne"] = "भित्र के छ" },
        ["about.feature_calendar"]     = new() { ["en"] = "BS / AD calendar with reminders",         ["ne"] = "BS / AD पात्रो र रिमाइन्डरहरू" },
        ["about.feature_converter"]    = new() { ["en"] = "Date & timezone converter",   ["ne"] = "मिति र टाइमजोन रूपान्तरण" },
        ["about.feature_banking"]      = new() { ["en"] = "Interest & EMI calculator",   ["ne"] = "ब्याज र EMI क्याल्कुलेटर" },
        ["about.feature_text"]         = new() { ["en"] = "Preeti ↔ Unicode, script tools & file conversion", ["ne"] = "प्रिती ↔ युनिकोड, लिपि टुल्स र फाइल रूपान्तरण" },
        ["about.feature_network"]      = new() { ["en"] = "Network tools: ping, scan, trace, WHOIS, DNS",      ["ne"] = "नेटवर्क टुल्स: पिङ, स्क्यान, ट्रेस, WHOIS, DNS" },
        ["about.feature_reminders"]    = new() { ["en"] = "Reminders & day notes",       ["ne"] = "रिमाइन्डर र नोटहरू" },
        ["about.feature_themes"]       = new() { ["en"] = "Themes & customization",      ["ne"] = "थिम र कस्टमाइज" },
        ["about.support_heading"]      = new() { ["en"] = "Support this project",        ["ne"] = "यो प्रोजेक्ट सपोर्ट गर्नुहोस्" },
        ["about.support_body"]         = new() { ["en"] = "NepDateWidget is free and always will be. If it saves you time, consider buying me a momo.",  ["ne"] = "NepDateWidget फ्री छ र सधैं रहनेछ। यसले समय बचायो भने, एउटा मोमो किनिदिनुहोस्।" },
        ["about.support_button"]       = new() { ["en"] = "Buy Me a Momo",               ["ne"] = "मोमो किनिदिनुहोस्" },
        ["about.links_heading"]        = new() { ["en"] = "Links",                       ["ne"] = "लिंकहरू" },
        ["about.repo_button"]          = new() { ["en"] = "View on GitHub",              ["ne"] = "GitHub मा हेर्नुहोस्" },
        ["about.built_by"]             = new() { ["en"] = "Built by Raju Prasai",        ["ne"] = "रजु प्रसाईले बनाएको" },

        // ── More tab ──────────────────────────────────────────────────────────
        ["tab.more"]                   = new() { ["en"] = "More",                        ["ne"] = "थप" },
        ["more.notes_heading"]         = new() { ["en"] = "Notes",                       ["ne"] = "नोटहरू" },
        ["more.reminders_heading"]     = new() { ["en"] = "Reminders",                   ["ne"] = "रिमाइन्डरहरू" },
        ["more.no_notes"]              = new() { ["en"] = "No notes yet",                ["ne"] = "अझै कुनै नोट छैन" },
        ["more.no_reminders"]          = new() { ["en"] = "No reminders yet",            ["ne"] = "अझै कुनै रिमाइन्डर छैन" },
        ["more.delete"]                = new() { ["en"] = "Delete",                      ["ne"] = "मेटाउनुहोस्" },
        ["more.edit"]                  = new() { ["en"] = "Edit",                        ["ne"] = "एडिट" },
        ["more.save"]                  = new() { ["en"] = "Save",                        ["ne"] = "सेभ गर्नुहोस्" },
        ["more.cancel"]                = new() { ["en"] = "Cancel",                      ["ne"] = "रद्द गर्नुहोस्" },

        // ── Window chrome tooltips ────────────────────────────────────────────
        ["tooltip.about"]              = new() { ["en"] = "About",                       ["ne"] = "बारेमा" },
        ["tooltip.pin"]                = new() { ["en"] = "Pin open",                    ["ne"] = "खुला राख्नुहोस्" },
        ["tooltip.unpin"]              = new() { ["en"] = "Unpin (auto-close)",          ["ne"] = "अनपिन (आफै बन्द हुन्छ)" },
        ["tooltip.minimize"]           = new() { ["en"] = "Minimize",                    ["ne"] = "सानो बनाउनुहोस्" },
        ["tooltip.settings"]           = new() { ["en"] = "Settings",                    ["ne"] = "सेटिङ" },
        ["tooltip.swap_tz"]            = new() { ["en"] = "Swap timezones",              ["ne"] = "टाइमजोन बदल्नुहोस्" },
        ["tooltip.toggle_format"]      = new() { ["en"] = "Toggle AM / PM",              ["ne"] = "AM / PM बदल्नुहोस्" },

        // ── Placeholder hints ─────────────────────────────────────────────────
        ["hint.principal"]             = new() { ["en"] = "e.g. 500000",                 ["ne"] = "जस्तै: ५००००" },
        ["hint.rate"]                  = new() { ["en"] = "e.g. 12",                     ["ne"] = "जस्तै: १२" },
        ["hint.months"]                = new() { ["en"] = "e.g. 120",                    ["ne"] = "जस्तै: १२०" },
        ["hint.loan"]                  = new() { ["en"] = "e.g. 5000000",                ["ne"] = "जस्तै: ५०००००" },
        ["hint.date_bs"]               = new() { ["en"] = "YYYY/MM/DD",                  ["ne"] = "YYYY/MM/DD" },
        ["hint.time"]                  = new() { ["en"] = "e.g. 2:30 PM",               ["ne"] = "जस्तै: २:३० PM" },
        ["hint.reminder_title"]        = new() { ["en"] = "What do you want to remember?", ["ne"] = "के याद राख्ने?" },
        ["hint.reminder_time"]         = new() { ["en"] = "9:00",                        ["ne"] = "९:००" },
        ["hint.area_value"]            = new() { ["en"] = "e.g. 1.5",                   ["ne"] = "जस्तै: १.५" },
        ["hint.weight_value"]          = new() { ["en"] = "e.g. 2.5",                   ["ne"] = "जस्तै: २.५" },

        // ── Settings choices (intentionally not localized: brand/style names) ─
        ["settings.lang_english"]      = new() { ["en"] = "English",                     ["ne"] = "English" },
        ["settings.lang_nepali"]       = new() { ["en"] = "नेपाली",                      ["ne"] = "नेपाली" },
        ["settings.theme_dark"]        = new() { ["en"] = "Dark",                        ["ne"] = "गाढा" },
        ["settings.theme_light"]       = new() { ["en"] = "Light",                       ["ne"] = "हल्का" },
        ["settings.corner_rounded"]    = new() { ["en"] = "Rounded",                     ["ne"] = "गोलो" },
        ["settings.corner_sharp"]      = new() { ["en"] = "Sharp",                       ["ne"] = "तीखो" },

        // ── Friendlier empty states ───────────────────────────────────────────
        ["more.no_notes_hint"]         = new() { ["en"] = "Tap any day on the calendar to add a note", ["ne"] = "नोट थप्न पात्रोमा कुनै दिन क्लिक गर्नुहोस्" },
        ["more.no_reminders_hint"]     = new() { ["en"] = "Tap any day on the calendar to set a reminder", ["ne"] = "रिमाइन्डर थप्न पात्रोमा कुनै दिन क्लिक गर्नुहोस्" },
    };

    // ── ILocalizationService ──────────────────────────────────────────────────

    public string CurrentLanguage => _language;

    public string Get(string key)
    {
        if (key is null)
            return "[]";

        if (_strings.TryGetValue(key, out var langs))
        {
            // Try active language first, then English as fallback
            if (langs.TryGetValue(_language, out var text) && !string.IsNullOrEmpty(text))
                return text;
            if (langs.TryGetValue("en", out var fallback) && !string.IsNullOrEmpty(fallback))
                return fallback;
        }

        // Key not found - return the key itself so missing strings are obvious in testing
        return $"[{key}]";
    }

    public void SetLanguage(string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            _language = languageCode.ToLowerInvariant();
    }
}
