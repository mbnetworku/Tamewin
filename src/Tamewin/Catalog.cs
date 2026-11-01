using System.Collections.Generic;
using System.Linq;

namespace Tamewin
{
    /// <summary>
    /// Every option the app offers. TO ADD AN OPTION: add one T(...) line below.
    /// Ids must be unique and never reused (backups are stored by id).
    /// </summary>
    public static class Catalog
    {
        public const string Tips = "Tips, suggestions & ads";
        public const string Search = "Search";
        public const string Typing = "Feedback, speech & typing";
        public const string Office = "Microsoft Office";
        public const string Diag = "Diagnostic data & telemetry";
        public const string Activity = "Activity, location & AI";
        public const string Features = "Windows features";
        public const string Updates = "Windows Update";
        public const string Speed = "Make PC faster";
        public const string Extras = "Handy extras";

        public static readonly string[] Groups = { Tips, Search, Typing, Office, Diag, Activity, Features, Extras, Updates, Speed };

        const string RestartExplorer = "Restart Explorer (or sign out) to see the change";
        const string RestartPc = "Restart the PC for this to take effect";
        const string SignOut = "Sign out and back in for this to take effect";

        const string CDM = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string ADV = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string SS = @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";
        const string DC = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
        const string OFFPOL = @"Software\Policies\Microsoft\Office";
        const string WPP = @"SOFTWARE\Policies\Microsoft\Windows NT\Printers\WPP";
        const string WU = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        const string NoServer = "http://127.0.0.1";   // update server that doesn't exist, so every check fails

        static RegOp CU(string key, string name, object value, object def = null) => new RegOp(Hive.CU, key, name, value, def);
        static RegOp LM(string key, string name, object value, object def = null) => new RegOp(Hive.LM, key, name, value, def);

        static Tweak T(string group, string id, string title, string info, params RegOp[] ops) =>
            new Tweak { Group = group, Id = id, Title = title, Info = info, Ops = ops };

        public static readonly List<Tweak> All = new List<Tweak>
        {
            // ---------------- Tips, suggestions & ads ----------------
            T(Tips, "tips", "Turn off tips and suggestions notifications",
                "Stops 'Get tips and suggestions when using Windows' pop-ups.",
                CU(CDM, "SubscribedContent-338389Enabled", 0), CU(CDM, "SoftLandingEnabled", 0)),
            T(Tips, "settings_ads", "Turn off suggested content in the Settings app",
                "Removes Microsoft promotions shown inside Settings.",
                CU(CDM, "SubscribedContent-338393Enabled", 0), CU(CDM, "SubscribedContent-353694Enabled", 0),
                CU(CDM, "SubscribedContent-353696Enabled", 0)),
            T(Tips, "start_suggestions", "Turn off suggestions and recommendations in Start",
                "Stops app suggestions and 'Recommended' promotions in the Start menu.",
                CU(CDM, "SubscribedContent-338388Enabled", 0), CU(CDM, "SystemPaneSuggestionsEnabled", 0),
                CU(ADV, "Start_IrisRecommendations", 0)),
            T(Tips, "silent_apps", "Stop Windows installing suggested apps by itself",
                "Stops Windows quietly installing promoted apps (games, social apps).",
                CU(CDM, "SilentInstalledAppsEnabled", 0)),
            T(Tips, "scoobe", "Turn off 'Finish setting up your device' screens",
                "Stops the full-screen reminders after updates that push Microsoft services.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0)),
            T(Tips, "ad_id", "Turn off advertising ID",
                "Apps can no longer use your advertising ID to show personalized ads.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)),
            T(Tips, "tailored", "Turn off tailored experiences",
                "Microsoft stops using your diagnostic data for personalized tips and ads.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)),
            T(Tips, "lang_list", "Don't let websites read my language list",
                "Websites can no longer see which languages you have installed.",
                CU(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1)),
            T(Tips, "track_apps", "Don't track which apps I open",
                "Windows stops recording app launches to improve Start and search.",
                CU(ADV, "Start_TrackProgs", 0)),

            // ---------------- Search ----------------
            T(Search, "search_history", "Turn off search history on this device",
                "Windows search stops keeping a history of what you searched.",
                CU(SS, "IsDeviceSearchHistoryEnabled", 0)),
            T(Search, "cloud_search", "Turn off cloud content search",
                "Start search stops searching your Microsoft / work account (OneDrive, Outlook, Bing).",
                CU(SS, "IsMSACloudSearchEnabled", 0), CU(SS, "IsAADCloudSearchEnabled", 0)),
            T(Search, "bing_search", "Turn off Bing web results in Start search",
                "What you type in Start is no longer sent to Bing.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)).Needs(SignOut),
            T(Search, "web_search_policy", "Block web results in Start search completely (policy)",
                "Stronger version of the above that Windows updates can't switch back on.",
                CU(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)).Needs(RestartExplorer),

            // ---------------- Feedback, speech & typing ----------------
            T(Typing, "feedback", "Never ask me for feedback",
                "Windows stops showing feedback surveys.",
                CU(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)),
            T(Typing, "speech", "Turn off online speech recognition",
                "Your voice is no longer sent to Microsoft for recognition. Voice typing may stop working.",
                CU(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0)),
            T(Typing, "typing", "Don't send typing and handwriting data",
                "Stops collecting what you type and write to build a personal dictionary.",
                CU(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                CU(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                CU(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                CU(@"Software\Microsoft\Input\TIPC", "Enabled", 0)),

            // ---------------- Office ----------------
            T(Office, "office_telemetry", "Turn off Office telemetry",
                "Word, Excel and the other Office apps stop sending diagnostic data.",
                CU(@"Software\Microsoft\Office\Common\ClientTelemetry", "DisableTelemetry", 1),
                CU(@"Software\Microsoft\Office\Common\ClientTelemetry", "SendTelemetry", 3)),
            T(Office, "office_telemetry_policy", "Office diagnostic data: none (policy)",
                "Enforces the above so Office can't switch it back on.",
                CU(OFFPOL + @"\Common\ClientTelemetry", "SendTelemetry", 3)),
            T(Office, "office_connected", "Turn off Office connected experiences",
                "Turns off online features that analyse or download content (Designer, online Editor suggestions, online pictures).",
                CU(OFFPOL + @"\16.0\Common\Privacy", "usercontentdisabled", 2),
                CU(OFFPOL + @"\16.0\Common\Privacy", "downloadcontentdisabled", 2),
                CU(OFFPOL + @"\16.0\Common\Privacy", "controllerconnectedservicesenabled", 2)),
            T(Office, "office_feedback", "Turn off Office feedback and surveys",
                "Removes 'Send feedback', screenshots in feedback, and surveys in Office.",
                CU(OFFPOL + @"\16.0\Common\Feedback", "enabled", 0),
                CU(OFFPOL + @"\16.0\Common\Feedback", "includescreenshot", 0),
                CU(OFFPOL + @"\16.0\Common\Feedback", "surveyenabled", 0)),

            // ---------------- Diagnostic data & telemetry ----------------
            T(Diag, "diag_level", "Diagnostic data: lowest level allowed",
                "Settings > Privacy > Diagnostics & feedback. Home/Pro can only go down to 'Required'; Enterprise/Education can turn it off. " +
                "Also hides feedback prompts and limits log and crash-dump collection. Settings will say 'managed by your organization'.",
                LM(DC, "AllowTelemetry", 0), LM(DC, "DoNotShowFeedbackNotifications", 1),
                LM(DC, "LimitDiagnosticLogCollection", 1), LM(DC, "LimitDumpCollection", 1)).Needs(RestartPc),
            T(Diag, "diag_services", "Disable the telemetry services",
                "Disables 'Connected User Experiences and Telemetry' (DiagTrack), which sends the diagnostic data, and the WAP push service.",
                LM(@"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, 2),
                LM(@"SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 4, 3)).Needs(RestartPc),
            T(Diag, "error_reporting", "Turn off Windows Error Reporting",
                "Crash reports are no longer sent to Microsoft.",
                LM(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)),
            T(Diag, "ceip", "Turn off Customer Experience Improvement Program",
                "Stops the older CEIP usage reporting.",
                LM(@"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0)),
            T(Diag, "app_telemetry", "Turn off app compatibility telemetry",
                "Stops the inventory of installed apps and app usage being collected.",
                LM(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableUAR", 1)),
            T(Diag, "consumer_features", "Block tailored experiences and consumer features (policy)",
                "Enforces 'tailored experiences' off and blocks promoted consumer apps.",
                CU(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1)),

            // ---------------- Activity, location & AI ----------------
            T(Activity, "activity", "Turn off activity history",
                "Windows stops collecting and uploading the apps, files and sites you use.",
                LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                LM(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0)),
            T(Activity, "ad_id_policy", "Block advertising ID for all users (policy)",
                "Enforces advertising ID off so it can't be switched back on.",
                LM(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1)),
            T(Activity, "recall", "Turn off Recall snapshots",
                "On Copilot+ PCs, Windows stops saving snapshots of your screen.",
                CU(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1)),
            T(Activity, "edge_data", "Turn off Microsoft Edge diagnostic data and personalization",
                "Edge stops sending browsing diagnostics and using your browsing for ads. Edge will say 'managed by your organization'.",
                LM(@"SOFTWARE\Policies\Microsoft\Edge", "DiagnosticData", 0),
                LM(@"SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0),
                LM(@"SOFTWARE\Policies\Microsoft\Edge", "UserFeedbackAllowed", 0)),
            T(Activity, "location", "Turn off location for all apps",
                "No app can get your location. Maps, weather, 'Find my device' and time-zone detection stop working.",
                LM(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny", "Allow"))
                .Optional(),

            // ---------------- Windows features ----------------
            T(Features, "classic_menu", "Use the old classic right-click menu",
                "Shows the full right-click menu straight away, without 'Show more options'.",
                CU(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", ""))
                .Needs(RestartExplorer).Optional(),
            T(Features, "wpp_on", "Windows protected print mode: force ON",
                "Only Mopria-compatible printers can be used (more secure printing). Registry values from community guides, not official docs.",
                LM(WPP, "EnabledBy", 2), LM(WPP, "WindowsProtectedPrintGroupPolicyState", 1),
                LM(WPP, "WindowsProtectedPrintMode", 1), LM(WPP, "WindowsProtectedPrintOobeConfigComplete", 1))
                .Warn("Printers that are not Mopria-compatible will be REMOVED. You'll need to add them again after turning this off.")
                .ConflictsWith("wpp_off").Needs(RestartPc).Optional(),
            T(Features, "wpp_off", "Windows protected print mode: force OFF",
                "All printers and printer drivers can be used. Registry values from community guides, not official docs.",
                LM(WPP, "WindowsProtectedPrintMode", 0), LM(WPP, "EnabledBy", null),
                LM(WPP, "WindowsProtectedPrintGroupPolicyState", null), LM(WPP, "WindowsProtectedPrintOobeConfigComplete", null))
                .ConflictsWith("wpp_on").Needs(RestartPc).Optional(),

            // ---------------- Handy extras ----------------
            T(Extras, "end_task", "Add 'End task' to the taskbar right-click menu",
                "Right-click any app on the taskbar and you get an 'End task' option that force-closes a frozen app without Task Manager.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\TaskbarDeveloperSettings", "TaskbarEndTask", 1, 0)).Optional(),
            T(Extras, "kbd_suggestions", "Turn on typing suggestions for the keyboard",
                "Windows shows word suggestions as you type on the normal (physical) keyboard, so you can finish words faster.",
                CU(@"Software\Microsoft\TabletTip\1.7", "EnableHwkbTextPrediction", 1, 0)).Optional(),

            // ---------------- Windows Update ----------------
            T(Updates, "block_updates", "Block Windows Update completely",
                "No Windows, security or driver updates are downloaded or installed, automatically or by hand. " +
                "Settings > Windows Update will say updates are managed by your organization. Revert turns updates back on.",
                LM(WU + @"\AU", "NoAutoUpdate", 1),
                LM(WU + @"\AU", "UseWUServer", 1),
                LM(WU, "WUServer", NoServer),
                LM(WU, "WUStatusServer", NoServer),
                LM(WU, "DoNotConnectToWindowsUpdateInternetLocations", 1),
                LM(WU, "DisableWindowsUpdateAccess", 1),
                LM(WU, "SetDisableUXWUAccess", 1),
                LM(WU, "ExcludeWUDriversInQualityUpdate", 1),
                LM(@"SYSTEM\CurrentControlSet\Services\wuauserv", "Start", 4, 3),
                LM(@"SYSTEM\CurrentControlSet\Services\UsoSvc", "Start", 4, 2))
                .Warn("Your PC will get NO security updates while this is on, which leaves it open to new viruses and attacks. " +
                      "Microsoft Store downloads and some Windows Defender updates may also stop. " +
                      "Windows may switch the update service back on by itself; if the status shows 'Partly', apply it again. " +
                      "Revert it at least once a month to install security updates.")
                .Needs(RestartPc).Optional(),

            // ---------------- Make PC faster ----------------
            T(Speed, "perf_visualfx", "Adjust Windows for best performance",
                "Sets Windows to 'best performance', which turns off most of the fancy visual effects (shadows, fades, smooth-scrolling).",
                CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2))
                .Needs(SignOut).Optional(),
            T(Speed, "perf_animations", "Turn off window animations",
                "Windows minimises, maximises and opens windows instantly instead of animating them. Feels snappier on slower PCs.",
                CU(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1")).Needs(SignOut).Optional(),
            T(Speed, "perf_menu_delay", "Remove the menu open delay",
                "Menus open the moment you hover, instead of after a short pause.",
                CU(@"Control Panel\Desktop", "MenuShowDelay", "0", "400")).Needs(SignOut).Optional(),
            T(Speed, "perf_transparency", "Turn off transparency effects",
                "The Start menu, taskbar and windows become solid instead of see-through, which uses less graphics power.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1)).Optional(),
            T(Speed, "perf_startup_delay", "Remove the delay before startup apps run",
                "Startup apps launch right away after sign-in, instead of after Windows' built-in pause.",
                CU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0)).Optional(),
            T(Speed, "perf_gamedvr", "Turn off Game DVR background recording",
                "Stops Windows recording your games in the background, which frees CPU and GPU. Xbox Game Bar still opens if you want it.",
                CU(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                CU(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1)).Optional(),
            T(Speed, "perf_search_index", "Turn off search indexing",
                "Disables the Windows Search service. Uses less disk and CPU, but searching in Start and File Explorer becomes much slower.",
                LM(@"SYSTEM\CurrentControlSet\Services\WSearch", "Start", 4, 2))
                .Warn("Searching your files in Start and File Explorer will become slow. Turn this back on if you rely on search.")
                .Needs(RestartPc).Optional(),
            T(Speed, "perf_sysmain", "Turn off SysMain (Superfetch)",
                "Disables the SysMain service, which preloads apps into memory. Can cut high disk usage on some PCs; on others apps may open a little slower.",
                LM(@"SYSTEM\CurrentControlSet\Services\SysMain", "Start", 4, 2))
                .Warn("On hard-drive (non-SSD) PCs some apps may open slightly slower. Safe to turn back on if you notice that.")
                .Needs(RestartPc).Optional(),
        };

        public static Tweak Find(string id) => All.FirstOrDefault(t => t.Id == id);
    }
}
