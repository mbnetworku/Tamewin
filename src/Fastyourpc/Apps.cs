using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Fastyourpc
{
    public enum AppLevel { Safe, Caution, Critical, Other }

    /// <summary>A known preinstalled app. Pattern is the package name; '*' matches anything.</summary>
    public sealed class KnownApp
    {
        public string Pattern, Name, Effect;
        public AppLevel Level;

        public bool IsMatch(string packageName) =>
            Regex.IsMatch(packageName, "^" + Regex.Escape(Pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
    }

    /// <summary>One row in the Apps tab: a known app (installed or not), or another installed app.</summary>
    public sealed class AppRow
    {
        public string Name, Effect;
        public AppLevel Level;
        public List<Package> Packages = new List<Package>();
        public bool Installed => Packages.Count > 0;
        public string FamilyName => Packages.FirstOrDefault()?.Id.FamilyName;
    }

    /// <summary>
    /// Known preinstalled apps and what removing them affects. TO ADD AN APP: add one A(...) line.
    /// Safe = nothing else depends on it. Caution = a Windows feature stops working. Critical = can break Windows features.
    /// </summary>
    public static class AppCatalog
    {
        static KnownApp A(AppLevel level, string pattern, string name, string effect) =>
            new KnownApp { Level = level, Pattern = pattern, Name = name, Effect = effect };

        const AppLevel S = AppLevel.Safe, C = AppLevel.Caution, X = AppLevel.Critical;

        public static readonly List<KnownApp> All = new List<KnownApp>
        {
            // ---------- Safe to remove ----------
            A(S, "Microsoft.BingNews", "Microsoft News", "Only the News app is removed."),
            A(S, "Microsoft.BingWeather", "Weather", "Only the Weather app is removed. Widgets weather still works."),
            A(S, "Microsoft.BingSearch", "Bing Search", "Only the Bing Search app is removed."),
            A(S, "Microsoft.GetHelp", "Get Help", "Some troubleshooter links that open Get Help won't work."),
            A(S, "Microsoft.Getstarted", "Tips", "Only the Tips app is removed."),
            A(S, "Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Only the Solitaire games are removed."),
            A(S, "Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office) hub", "Only the launcher. Installed Word, Excel, etc. keep working."),
            A(S, "Microsoft.People", "People", "Only the People app is removed."),
            A(S, "Microsoft.WindowsFeedbackHub", "Feedback Hub", "You can't send feedback to Microsoft."),
            A(S, "Microsoft.ZuneVideo", "Movies & TV", "Videos open in another player (e.g. Media Player)."),
            A(S, "Microsoft.WindowsMaps", "Maps", "Offline maps and the Maps app are removed."),
            A(S, "Microsoft.MixedReality.Portal", "Mixed Reality Portal", "Only needed for VR headsets."),
            A(S, "Microsoft.SkypeApp", "Skype", "Skype calls and chat from this app stop."),
            A(S, "Clipchamp.Clipchamp", "Clipchamp video editor", "Only the video editor is removed."),
            A(S, "Microsoft.Todos", "Microsoft To Do", "Your tasks stay in your Microsoft account online."),
            A(S, "Microsoft.PowerAutomateDesktop", "Power Automate", "Your automation flows on this PC stop running."),
            A(S, "Microsoft.549981C3F5F10", "Cortana", "Cortana is removed."),
            A(S, "MicrosoftTeams", "Microsoft Teams (personal, old)", "Personal Teams chat stops. Work Teams is a separate app."),
            A(S, "MSTeams", "Microsoft Teams", "Teams chat and meetings from this app stop."),
            A(S, "Microsoft.Windows.DevHome", "Dev Home", "Only the developer dashboard is removed."),
            A(S, "Microsoft.Copilot", "Copilot", "The Copilot app and Copilot key stop working."),
            A(S, "Microsoft.Windows.Ai.Copilot.Provider", "Copilot provider", "Copilot integration in Windows stops."),
            A(S, "7EE7776C.LinkedInforWindows", "LinkedIn", "Only the LinkedIn app is removed."),
            A(S, "SpotifyAB.SpotifyMusic", "Spotify", "Only the Spotify app is removed. Your account isn't affected."),
            A(S, "Disney.*", "Disney+", "Only the Disney+ app is removed."),
            A(S, "AmazonVideo.PrimeVideo", "Prime Video", "Only the Prime Video app is removed."),
            A(S, "BytedancePte.Ltd.TikTok", "TikTok", "Only the TikTok app is removed."),
            A(S, "Facebook.*", "Facebook / Instagram / Messenger", "Only these apps are removed. Your accounts aren't affected."),
            A(S, "king.com.*", "Candy Crush and other King games", "Only the games are removed."),
            A(S, "*.Netflix", "Netflix", "Only the Netflix app is removed."),

            // ---------- Think first ----------
            A(C, "Microsoft.ZuneMusic", "Media Player", "Windows' default music/video player. You'll need another player (e.g. VLC)."),
            A(C, "Microsoft.YourPhone", "Phone Link", "You can't connect your phone to this PC (calls, texts, photos)."),
            A(C, "Microsoft.WindowsAlarms", "Clock", "Alarms, timers and Focus sessions stop working."),
            A(C, "Microsoft.MicrosoftStickyNotes", "Sticky Notes", "Notes not synced to your Microsoft account may be lost."),
            A(C, "Microsoft.WindowsCamera", "Camera", "The Camera app is removed. Video-call apps still use the webcam."),
            A(C, "Microsoft.Windows.Photos", "Photos", "Windows' default picture viewer. Pictures open in another app."),
            A(C, "Microsoft.WindowsCalculator", "Calculator", "The Calculator is removed."),
            A(C, "Microsoft.Paint", "Paint", "Paint is removed."),
            A(C, "Microsoft.WindowsNotepad", "Notepad", ".txt files and 'Edit' actions need another text editor."),
            A(C, "Microsoft.ScreenSketch", "Snipping Tool", "Win+Shift+S and Print Screen snipping stop working."),
            A(C, "Microsoft.WindowsSoundRecorder", "Sound Recorder", "You can't record audio without another app."),
            A(C, "Microsoft.OutlookForWindows", "Outlook (new)", "If you read email in new Outlook, you lose access until you reinstall it."),
            A(C, "microsoft.windowscommunicationsapps", "Mail and Calendar (old)", "The old Mail and Calendar apps stop working."),
            A(C, "Microsoft.GamingApp", "Xbox app", "Game Pass PC games can't be installed or launched."),
            A(C, "Microsoft.XboxApp", "Xbox Console Companion", "Xbox console features on PC stop."),
            A(C, "Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Win+G overlay and game screen recording stop."),
            A(C, "Microsoft.Xbox.TCUI", "Xbox TCUI", "Some games' Xbox sign-in and friends screens stop working."),
            A(C, "Microsoft.XboxSpeechToTextOverlay", "Xbox speech-to-text", "Game chat transcription stops."),
            A(C, "MicrosoftCorporationII.QuickAssist", "Quick Assist", "You can't give or get remote help with Quick Assist."),
            A(C, "MicrosoftCorporationII.MicrosoftFamily", "Microsoft Family Safety", "Family Safety controls on this PC stop."),
            A(C, "MicrosoftWindows.Client.WebExperience", "Widgets", "The Widgets board and taskbar weather disappear."),
            A(C, "Microsoft.WindowsTerminal", "Terminal", "Command-line users need another console."),
            A(C, "Microsoft.HEIFImageExtension", "HEIF image codec", "iPhone photos (.heic) won't open."),
            A(C, "Microsoft.HEVCVideoExtension*", "HEVC video codec", "Many phone videos (.mov / H.265) won't play."),
            A(C, "Microsoft.WebpImageExtension", "WebP image codec", "WebP pictures from the web won't open in Photos."),
            A(C, "Microsoft.VP9VideoExtensions", "VP9 video codec", "Some web videos won't play in Windows apps."),
            A(C, "Microsoft.RawImageExtension", "Raw image codec", "Camera RAW photos won't open."),
            A(C, "Microsoft.AV1VideoExtension", "AV1 video codec", "AV1 videos won't play in Windows apps."),
            A(C, "Microsoft.MPEG2VideoExtension", "MPEG-2 video codec", "MPEG-2 videos (DVD rips) won't play."),

            // ---------- Critical: not recommended ----------
            A(X, "Microsoft.WindowsStore", "Microsoft Store", "You can't install or update Store apps, or reinstall anything removed here. Very hard to restore."),
            A(X, "Microsoft.StorePurchaseApp", "Store purchases", "Buying and licensing Store apps breaks."),
            A(X, "Microsoft.DesktopAppInstaller", "App Installer (winget)", "winget and .msix/.appx installers stop working."),
            A(X, "Microsoft.SecHealthUI", "Windows Security", "You can't open Windows Security to check virus protection."),
            A(X, "Microsoft.XboxIdentityProvider", "Xbox Identity Provider", "Sign-in for Xbox and many PC games breaks."),
            A(X, "Microsoft.AAD.BrokerPlugin", "Work or school account sign-in", "Signing in to work/school accounts and Office can break."),
            A(X, "Microsoft.Windows.CloudExperienceHost", "Windows sign-in & setup screens", "Account sign-in and setup screens can break."),
            A(X, "Microsoft.LanguageExperiencePack*", "Windows language pack", "Your Windows display language can break."),
            A(X, "Microsoft.VCLibs*", "Visual C++ runtime for apps", "Many apps that need it stop opening."),
            A(X, "Microsoft.UI.Xaml*", "Windows UI library", "Many apps that need it stop opening."),
            A(X, "Microsoft.NET.Native*", ".NET runtime for apps", "Many apps that need it stop opening."),
            A(X, "Microsoft.WindowsAppRuntime*", "Windows App Runtime", "Many newer apps stop opening."),
        };

        public static string LevelTitle(AppLevel l) =>
            l == AppLevel.Safe ? "Safe to remove" :
            l == AppLevel.Caution ? "Think first - a feature stops working" :
            l == AppLevel.Critical ? "CRITICAL - not recommended" :
            "Other installed apps (not in our list)";
    }

    /// <summary>Finds installed apps and uninstalls them, using Windows' own PackageManager (no PowerShell).</summary>
    public static class AppScanner
    {
        static readonly string RemovedLog = Path.Combine(Engine.DataFolder, "removed-apps.tsv");

        /// <summary>Every known app (installed or not) plus other removable apps installed for this user.</summary>
        public static List<AppRow> Scan()
        {
            var packages = new PackageManager().FindPackagesForUser("").ToList();
            var rows = new List<AppRow>();
            var claimed = new HashSet<string>();

            // Group known apps by name, so e.g. all "Facebook.*" packages become one row.
            foreach (var group in AppCatalog.All.GroupBy(a => a.Name))
            {
                var app = group.First();
                var row = new AppRow { Name = app.Name, Level = app.Level, Effect = app.Effect };
                row.Packages = packages.Where(p => group.Any(a => a.IsMatch(p.Id.Name))).ToList();
                foreach (var p in row.Packages) claimed.Add(p.Id.FullName);
                rows.Add(row);
            }

            // Anything else the user (or the PC maker) installed from a store, that Windows lets you remove.
            foreach (var p in packages.Where(p => !claimed.Contains(p.Id.FullName) && !p.IsFramework &&
                                                  !p.IsResourcePackage && !p.IsBundle && IsUserRemovable(p)))
            {
                string name;
                try { name = p.DisplayName; } catch { name = null; }
                rows.Add(new AppRow
                {
                    Name = string.IsNullOrWhiteSpace(name) || name.StartsWith("ms-resource:") ? p.Id.Name : name + "  (" + p.Id.Name + ")",
                    Level = AppLevel.Other,
                    Effect = "Not in our list. Check what it is before removing.",
                    Packages = { p },
                });
            }
            return rows;
        }

        static bool IsUserRemovable(Package p)
        {
            try { return p.SignatureKind == PackageSignatureKind.Store || p.SignatureKind == PackageSignatureKind.Developer; }
            catch { return false; }
        }

        /// <summary>Uninstalls every package of the row for the current user. Returns an error message, or null on success.</summary>
        public static async Task<string> UninstallAsync(AppRow row)
        {
            var pm = new PackageManager();
            foreach (var p in row.Packages)
            {
                try
                {
                    var result = await pm.RemovePackageAsync(p.Id.FullName).AsTask();
                    if (result.ExtendedErrorCode != null) return result.ErrorText;
                }
                catch (Exception ex) { return ex.Message; }
                LogRemoved(row.Name, p.Id.FamilyName);
            }
            return null;
        }

        static void LogRemoved(string name, string familyName)
        {
            Directory.CreateDirectory(Engine.DataFolder);
            File.AppendAllText(RemovedLog, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\t" + name + "\t" + familyName + Environment.NewLine);
        }

        /// <summary>Opens the Microsoft Store page to get an app back.</summary>
        public static void OpenInStore(AppRow row)
        {
            var family = row.FamilyName ?? RemovedFamilyName(row.Name);
            var uri = family != null
                ? "ms-windows-store://pdp/?PFN=" + Uri.EscapeDataString(family)
                : "ms-windows-store://search/?query=" + Uri.EscapeDataString(row.Name);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
        }

        static string RemovedFamilyName(string name)
        {
            if (!File.Exists(RemovedLog)) return null;
            return File.ReadAllLines(RemovedLog).Select(l => l.Split('\t'))
                .LastOrDefault(f => f.Length == 3 && f[1] == name)?[2];
        }
    }
}
