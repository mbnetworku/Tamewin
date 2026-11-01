using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Tamewin.Theme;

namespace Tamewin
{
    /// <summary>Main window: sidebar on the left, one page shown at a time on the right.</summary>
    public sealed class MainForm : Form
    {
        readonly Engine engine;
        readonly Panel pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        readonly Label status = new Label
        {
            Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            Padding = new Padding(S(30), 0, S(30), 0), Margin = Padding.Empty,
        };
        readonly TextBox logBox = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5f),
        };
        readonly List<(NavItem nav, Control page)> pages = new List<(NavItem, Control)>();
        readonly TweakPage privacyPage, featuresPage, speedPage;
        readonly AppsPage appsPage;
        readonly Control startPage;
        readonly StartSettings startSettings = StartSettings.Load();

        readonly bool showTestLabel;

        /// <param name="showTestLabel">false hides "TEST MODE" (used for screenshots).</param>
        public MainForm(Engine engine, bool showTestLabel = true)
        {
            this.engine = engine;
            this.showTestLabel = showTestLabel;
            AutoScaleMode = AutoScaleMode.None;
            Font = Body;
            BackColor = Window;
            ForeColor = Theme.Text;
            Text = "Tamewin" + (engine.TestMode && showTestLabel ? "  [TEST MODE]" : "");
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* no icon: use the default */ }
            Size = new Size(S(1160), S(800));
            MinimumSize = new Size(S(860), S(580));
            StartPosition = FormStartPosition.CenterScreen;
            status.Font = Theme.Desc;
            status.ForeColor = TextDim;
            logBox.BackColor = Card;
            logBox.ForeColor = Theme.Text;

            privacyPage = new TweakPage(engine, Log, "Privacy",
                "Turn off what Windows, Office and Edge send to Microsoft. Revert puts back exactly what was there before.",
                Catalog.Groups.Where(g => g != Catalog.Features && g != Catalog.Updates && g != Catalog.Speed));
            speedPage = new TweakPage(engine, Log, "Make PC faster",
                "Turn off visual effects and background work to speed up a slower PC. Every change can be reverted.",
                new[] { Catalog.Speed });
            featuresPage = new TweakPage(engine, Log, "Windows features",
                "Classic right-click menu, handy extras, protected print mode and Windows Update.",
                new[] { Catalog.Features, Catalog.Extras, Catalog.Updates },
                new FlatButton("Restart Explorer", ButtonKind.Subtle, (s, e) => RestartExplorer()),
                new FlatButton("Open printer settings", ButtonKind.Subtle,
                    (s, e) => Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true })),
                new FlatButton("Switch to a local account", ButtonKind.Subtle, (s, e) => SwitchToLocalAccount()));
            appsPage = new AppsPage(Log);
            startPage = BuildStartPage();

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Window };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(256)));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildSidebar(), 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Window };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, S(30)));
            right.Controls.Add(pageHost, 0, 0);
            right.Controls.Add(status, 0, 1);
            root.Controls.Add(right, 1, 0);
            Controls.Add(root);

            Log("Ready. Nothing changes until you click Apply or Uninstall.");
            ShowPage("privacy");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            ApplyTitleBar(Handle);
            base.OnHandleCreated(e);
        }

        // ---------------- sidebar ----------------

        Control BuildSidebar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Sidebar,
                Padding = new Padding(S(16), S(20), S(16), S(16)), Margin = Padding.Empty,
            };
            bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var logo = new PictureBox
            {
                Size = new Size(S(28), S(28)), SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(S(4), S(1), S(8), 0), BackColor = Color.Transparent,
            };
            try { logo.Image = Icon?.ToBitmap(); } catch { /* no icon: show text only */ }
            var title = new Label
            {
                AutoSize = true, Text = "Tamewin", Font = AppTitle, ForeColor = Theme.Text,
                Margin = new Padding(0, S(2), 0, 0),
            };
            var brand = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(S(2), 0, 0, S(2)) };
            brand.Controls.Add(logo);
            brand.Controls.Add(title);
            var version = new Label
            {
                AutoSize = true, Font = Small, ForeColor = TextDim, Margin = new Padding(S(7), 0, 0, S(22)),
                Text = "Version " + Application.ProductVersion + (engine.TestMode && showTestLabel ? "  -  TEST MODE" : ""),
            };
            var head = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            head.Controls.Add(brand);
            head.Controls.Add(version);
            bar.Controls.Add(head, 0, 0);

            var navList = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            AddPage(navList, "privacy", IconShield, "Privacy", privacyPage);
            AddPage(navList, "speed", IconRocket, "Make PC faster", speedPage);
            AddPage(navList, "start", IconStart, "Start menu", startPage);
            AddPage(navList, "features", IconSettings, "Windows features", featuresPage);
            AddPage(navList, "apps", IconApps, "Apps (bloatware)", appsPage);
            AddPage(navList, "log", IconHistory, "Activity", BuildLogPage());
            bar.Controls.Add(navList, 0, 1);

            bar.Controls.Add(BuildAdminCard(), 0, 3);
            return bar;
        }

        void AddPage(FlowLayoutPanel navList, string key, string glyph, string text, Control page)
        {
            var nav = new NavItem(glyph, text) { Tag = key };
            nav.Click += (s, e) => ShowPage(key);
            navList.Controls.Add(nav);
            page.Visible = false;
            pageHost.Controls.Add(page);
            pages.Add((nav, page));
        }

        public void ShowPage(string key)
        {
            foreach (var (nav, page) in pages)
            {
                bool on = (string)nav.Tag == key;
                nav.Selected = on;
                page.Visible = on;
            }
            if (key == "apps") appsPage.ScanIfNeeded();
            if (key == "privacy") privacyPage.RefreshStates();
            if (key == "speed") speedPage.RefreshStates();
            if (key == "features") featuresPage.RefreshStates();
        }

        Control BuildAdminCard()
        {
            var card = new FlowLayoutPanel
            {
                AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = Card, Padding = new Padding(S(14)), Margin = Padding.Empty, Width = S(224),
            };
            card.Controls.Add(new Label
            {
                AutoSize = true, Font = BodyBold, ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, S(4)),
                Text = Engine.IsAdmin ? "Administrator" : "Standard user",
            });
            card.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(S(196), 0), Font = Theme.Desc, ForeColor = TextDim,
                Margin = new Padding(0, 0, 0, Engine.IsAdmin ? 0 : S(10)),
                Text = Engine.IsAdmin
                    ? "Every option is available."
                    : "Options marked 'Needs admin' are locked. Restart as Administrator to use them.",
            });
            if (!Engine.IsAdmin)
                card.Controls.Add(new FlatButton("Restart as admin", ButtonKind.Secondary, (s, e) => RestartAsAdmin(), IconAdmin)
                    { Margin = Padding.Empty });
            return card;
        }

        void RestartAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch { Log("Restart as Administrator was cancelled."); }
        }

        // ---------------- Start menu page ----------------

        Control BuildStartPage()
        {
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoScroll = true, BackColor = Window, Padding = new Padding(S(30), S(4), S(30), S(20)), Margin = Padding.Empty,
            };

            Label Section(string t) => new Label { Text = t, Font = Theme.Section, ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(0, S(14), 0, S(6)) };
            Label Note(string t) => new Label { Text = t, Font = Desc, ForeColor = TextDim, AutoSize = true, Margin = new Padding(0, 0, 0, S(8)) };

            content.Controls.Add(Note("A Windows 7-style launcher with your real programs, shortcuts, search and a power button. " +
                                      "It opens from here. It does not replace the real Start button."));

            content.Controls.Add(Section("Style"));
            var glossy = new RadioButton { Text = "Glossy (Aero)", Font = Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent, Checked = startSettings.Style == StartStyle.Glossy, Margin = new Padding(0, S(2), 0, S(2)) };
            var glass = new RadioButton { Text = "Liquid glass (blur)", Font = Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent, Checked = startSettings.Style == StartStyle.LiquidGlass, Margin = new Padding(0, S(2), 0, S(2)) };
            glossy.CheckedChanged += (s, e) => { if (glossy.Checked) { startSettings.Style = StartStyle.Glossy; startSettings.Save(); } };
            glass.CheckedChanged += (s, e) => { if (glass.Checked) { startSettings.Style = StartStyle.LiquidGlass; startSettings.Save(); } };
            content.Controls.Add(glossy);
            content.Controls.Add(glass);

            content.Controls.Add(Section("Shown on the right"));
            content.Controls.Add(Note("Untick to hide a shortcut from the menu."));
            foreach (var sc in Shortcut.Standard())
            {
                var cb = new CheckBox { Text = sc.Label, Font = Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent, Checked = !startSettings.Hidden.Contains(sc.Key), Margin = new Padding(0, S(2), 0, S(2)) };
                var key = sc.Key;
                cb.CheckedChanged += (s, e) =>
                {
                    if (cb.Checked) startSettings.Hidden.Remove(key); else startSettings.Hidden.Add(key);
                    startSettings.Save();
                };
                content.Controls.Add(cb);
            }

            var page = new PageLayout("Start menu", "A Windows 7-style launcher you open from Tamewin.", content);
            page.Footer.Summary.Text = "Your choices are saved automatically.";
            page.Footer.Buttons.Controls.Add(new FlatButton("Open Start menu", ButtonKind.Primary, (s, e) => OpenStartMenu(), IconStart));
            return page;
        }

        void OpenStartMenu()
        {
            try { new StartMenuForm(startSettings).Show(); }
            catch (Exception ex) { Log("Could not open the Start menu: " + ex.Message); }
        }

        // ---------------- activity page ----------------

        Control BuildLogPage()
        {
            var holder = new Panel { Dock = DockStyle.Fill, BackColor = Window, Padding = new Padding(S(30), S(4), S(30), S(16)), Margin = Padding.Empty };
            var frame = new Panel { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(S(12)) };
            frame.Controls.Add(logBox);
            holder.Controls.Add(frame);
            var page = new PageLayout("Activity", "Everything this app changed in this session.", holder);
            page.Footer.Summary.Text = "Backups and the list of removed apps are kept in " + Engine.DataFolder;
            page.Footer.Buttons.Controls.Add(new FlatButton("Open data folder", ButtonKind.Secondary, (s, e) =>
            {
                System.IO.Directory.CreateDirectory(Engine.DataFolder);
                Process.Start(new ProcessStartInfo(Engine.DataFolder) { UseShellExecute = true });
            }));
            return page;
        }

        void Log(string line)
        {
            logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "   " + line + Environment.NewLine);
            status.Text = line;
        }

        void RestartExplorer()
        {
            if (MessageBox.Show(this, "Restart Explorer now? Open File Explorer windows will close.", "Tamewin",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
            System.Threading.Thread.Sleep(1500);
            if (Process.GetProcessesByName("explorer").Length == 0) Process.Start("explorer.exe");
            Log("Explorer restarted.");
        }

        /// <summary>Opens the Windows page where the user switches a Microsoft account to a local one.
        /// Tamewin never sees the password or changes the account itself.</summary>
        void SwitchToLocalAccount()
        {
            if (MessageBox.Show(this,
                    "This opens Windows Settings, where you can switch from a Microsoft account to a local account.\n\n" +
                    "On the page that opens, click \"Sign in with a local account instead\" and follow Windows' steps. " +
                    "You'll set a local username and password.\n\n" +
                    "Tamewin does not change your account or see your password - Windows does it all. Open Settings now?",
                    "Switch to a local account", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            try { Process.Start(new ProcessStartInfo("ms-settings:yourinfo") { UseShellExecute = true }); }
            catch { Process.Start(new ProcessStartInfo("ms-settings:otherusers") { UseShellExecute = true }); }
            Log("Opened Settings for switching to a local account.");
        }
    }

    /// <summary>A page of tweaks from some catalog groups, with Apply / Revert.</summary>
    sealed class TweakPage : PageLayout
    {
        readonly Engine engine;
        readonly Action<string> log;
        readonly List<OptionRow> rows = new List<OptionRow>();
        readonly FlatButton applyBtn, revertBtn;

        public TweakPage(Engine engine, Action<string> log, string title, string subtitle, IEnumerable<string> groups,
                         params FlatButton[] extraButtons) : base(title, subtitle)
        {
            this.engine = engine;
            this.log = log;

            var groupList = groups.ToList();
            if (Catalog.All.Any(t => groupList.Contains(t.Group) && !t.OptIn))
                Toolbar.Controls.Add(new FlatButton("Select recommended", ButtonKind.Subtle,
                    (s, e) => SelectWhere(t => !t.OptIn)));
            Toolbar.Controls.Add(new FlatButton("Select applied", ButtonKind.Subtle,
                (s, e) => SelectWhere(t => engine.State(t) != TweakState.NotApplied)));
            Toolbar.Controls.Add(new FlatButton("Clear selection", ButtonKind.Subtle, (s, e) => SelectWhere(t => false)));
            Toolbar.Controls.AddRange(extraButtons);

            foreach (var group in groupList)
            {
                var tweaks = Catalog.All.Where(t => t.Group == group).ToList();
                if (tweaks.Count == 0) continue;
                if (groupList.Count > 1) List.Controls.Add(new SectionHeader(group));
                foreach (var t in tweaks)
                {
                    var desc = t.Info;
                    if (t.Warning != null) desc += "  Warning: " + t.Warning;
                    if (t.Note != null) desc += "  (" + t.Note + ")";
                    var row = new OptionRow { Title = t.Title, Description = desc, Item = t };
                    if (t.Warning != null)
                        row.ConfirmCheck = () => MessageBox.Show(FindForm(), t.Warning + "\n\nSelect it anyway?", t.Title,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                    row.CheckedChanged += (s, e) => UpdateSummary();
                    rows.Add(row);
                    List.Controls.Add(row);
                }
            }

            revertBtn = new FlatButton("Revert selected", ButtonKind.Secondary, (s, e) => Run(apply: false));
            applyBtn = new FlatButton("Apply selected", ButtonKind.Primary, (s, e) => Run(apply: true));
            Footer.Buttons.Controls.Add(applyBtn);
            Footer.Buttons.Controls.Add(revertBtn);
            RefreshStates();
        }

        void SelectWhere(Func<Tweak, bool> pick)
        {
            foreach (var r in rows) r.Checked = !r.Locked && pick((Tweak)r.Item);
        }

        public void RefreshStates()
        {
            foreach (var row in rows)
            {
                var t = (Tweak)row.Item;
                var state = engine.State(t);
                row.Locked = !engine.CanRun(t);
                if (row.Locked) row.Checked = false;
                row.Pills.Clear();
                if (t.Admin) row.Pills.Add(engine.CanRun(t) ? Blue("Admin") : Gray("Needs admin"));
                row.Pills.Add(state == TweakState.Applied ? Green("Applied") : state == TweakState.Partly ? Amber("Partly") : Gray("Not applied"));
                row.Invalidate();
            }
            List.PerformLayout();
            UpdateSummary();
        }

        void UpdateSummary()
        {
            int applied = rows.Count(r => engine.State((Tweak)r.Item) == TweakState.Applied);
            int selected = rows.Count(r => r.Checked);
            Footer.Summary.Text = applied + " of " + rows.Count + " applied     " + selected + " selected";
            applyBtn.Enabled = revertBtn.Enabled = selected > 0;
        }

        void Run(bool apply)
        {
            var chosen = rows.Where(r => r.Checked).Select(r => (Tweak)r.Item).ToList();
            if (chosen.Count == 0) return;
            if (apply && chosen.Any(t => t.Id == "wpp_on") && chosen.Any(t => t.Id == "wpp_off"))
            {
                MessageBox.Show(FindForm(), "Choose either protected print ON or OFF, not both.", "Tamewin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var verb = apply ? "Apply" : "Revert";
            if (MessageBox.Show(FindForm(), verb + " " + chosen.Count + " setting(s)?\n\n" +
                    string.Join("\n", chosen.Select(t => "-  " + t.Title)), "Tamewin",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (var t in chosen)
                if (apply) engine.Apply(t, log); else engine.Revert(t, log);
            SelectWhere(t => false);
            RefreshStates();

            var notes = chosen.Where(t => t.Note != null).Select(t => t.Note).Distinct().ToList();
            log(verb + " finished: " + chosen.Count + " setting(s)." + (notes.Count > 0 ? " " + string.Join(". ", notes) + "." : ""));
            if (notes.Count > 0)
                MessageBox.Show(FindForm(), "Done.\n\n" + string.Join("\n", notes), "Tamewin", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>Scans preinstalled apps and uninstalls the ones the user picks.</summary>
    sealed class AppsPage : PageLayout
    {
        readonly Action<string> log;
        readonly TextBox search = new TextBox { Width = S(280), BorderStyle = BorderStyle.FixedSingle, Font = Body };
        readonly FlatButton showBtn, uninstallBtn, storeBtn;
        readonly Dictionary<SectionHeader, List<OptionRow>> sections = new Dictionary<SectionHeader, List<OptionRow>>();
        OptionRow highlighted;
        bool installedOnly, scanned;

        public AppsPage(Action<string> log) : base("Apps",
            "Preinstalled apps on this PC. Click an app to see what stops working if you remove it.")
        {
            this.log = log;
            search.BackColor = Card;
            search.ForeColor = Theme.Text;
            search.Margin = new Padding(S(4), S(6), S(12), S(4));
            search.SetPlaceholder("Search apps");
            search.TextChanged += (s, e) => ApplyFilter();

            showBtn = new FlatButton("Show: all apps", ButtonKind.Subtle, (s, e) =>
            {
                installedOnly = !installedOnly;
                showBtn.Text = installedOnly ? "Show: installed only" : "Show: all apps";
                ApplyFilter();
            });
            Toolbar.Controls.Add(search);
            Toolbar.Controls.Add(showBtn);
            Toolbar.Controls.Add(new FlatButton("Select all safe", ButtonKind.Subtle,
                (s, e) => ForRows(r => r.Checked = !r.Locked && ((AppRow)r.Item).Level == AppLevel.Safe)));
            Toolbar.Controls.Add(new FlatButton("Clear selection", ButtonKind.Subtle, (s, e) => ForRows(r => r.Checked = false)));
            Toolbar.Controls.Add(new FlatButton("Scan again", ButtonKind.Subtle, (s, e) => Scan()));

            uninstallBtn = new FlatButton("Uninstall selected", ButtonKind.Primary, async (s, e) => await UninstallSelected());
            storeBtn = new FlatButton("Get from Store", ButtonKind.Secondary, (s, e) =>
            {
                if (highlighted != null) AppScanner.OpenInStore((AppRow)highlighted.Item);
            });
            Footer.Buttons.Controls.Add(uninstallBtn);
            Footer.Buttons.Controls.Add(storeBtn);
            UpdateSummary();
        }

        void ForRows(Action<OptionRow> a) { foreach (var r in sections.Values.SelectMany(x => x)) a(r); }

        public void ScanIfNeeded() { if (!scanned) Scan(); }

        public void Scan()
        {
            scanned = true;
            log("Scanning installed apps...");
            List<AppRow> found;
            try { found = AppScanner.Scan(); }
            catch (Exception ex) { log("Scan failed: " + ex.Message); return; }

            highlighted = null;
            List.SuspendLayout();
            foreach (Control c in List.Controls.Cast<Control>().ToList()) c.Dispose();
            sections.Clear();

            foreach (var level in found.Select(r => r.Level).Distinct().OrderBy(l => l))
            {
                var inLevel = found.Where(r => r.Level == level).OrderBy(r => !r.Installed).ThenBy(r => r.Name).ToList();
                var header = new SectionHeader(AppCatalog.LevelTitle(level))
                {
                    Count = level == AppLevel.Other
                        ? inLevel.Count + " installed"
                        : inLevel.Count(r => r.Installed) + " of " + inLevel.Count + " installed",
                };
                List.Controls.Add(header);
                var list = new List<OptionRow>();
                foreach (var app in inLevel)
                {
                    var row = new OptionRow { Title = app.Name, Description = app.Effect, Item = app, Locked = !app.Installed };
                    if (app.Level == AppLevel.Critical) row.Pills.Add(Red("Critical"));
                    else if (app.Level == AppLevel.Caution) row.Pills.Add(Amber("Think first"));
                    else if (app.Level == AppLevel.Safe) row.Pills.Add(Blue("Safe"));
                    row.Pills.Add(app.Installed ? Green("Installed") : Gray("Not installed"));
                    if (app.Level == AppLevel.Critical)
                        row.ConfirmCheck = () => MessageBox.Show(FindForm(),
                            app.Name + " is CRITICAL.\n\nIf you remove it: " + app.Effect +
                            "\n\nRemoving it is NOT recommended. Select it anyway?", "Critical app",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                    row.CheckedChanged += (s, e) => UpdateSummary();
                    row.Activated += (s, e) =>
                    {
                        if (highlighted != null) highlighted.Highlighted = false;
                        highlighted = row;
                        row.Highlighted = true;
                        UpdateSummary();
                    };
                    list.Add(row);
                    List.Controls.Add(row);
                }
                sections[header] = list;
            }
            List.ResumeLayout();
            ApplyFilter();

            var known = found.Where(r => r.Level != AppLevel.Other).ToList();
            log("Scan done: " + known.Count(r => r.Installed) + " of " + known.Count + " known apps installed (" +
                known.Count(r => r.Installed && r.Level == AppLevel.Safe) + " safe to remove), " +
                found.Count(r => r.Level == AppLevel.Other) + " other apps.");
        }

        void ApplyFilter()
        {
            var text = search.Text.Trim();
            List.SuspendLayout();
            foreach (var kv in sections)
            {
                bool any = false;
                foreach (var row in kv.Value)
                {
                    var app = (AppRow)row.Item;
                    bool show = (!installedOnly || app.Installed) &&
                                (text.Length == 0 ||
                                 app.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 app.Effect.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
                    row.Visible = show;
                    any |= show;
                }
                kv.Key.Visible = any;
            }
            List.ResumeLayout();
            List.PerformLayout();
            UpdateSummary();
        }

        void UpdateSummary()
        {
            var all = sections.Values.SelectMany(x => x).ToList();
            int installed = all.Count(r => ((AppRow)r.Item).Installed);
            int safe = all.Count(r => ((AppRow)r.Item).Installed && ((AppRow)r.Item).Level == AppLevel.Safe);
            int selected = all.Count(r => r.Checked);
            Footer.Summary.Text = scanned
                ? installed + " installed     " + safe + " safe to remove     " + selected + " selected"
                : "Not scanned yet";
            uninstallBtn.Enabled = selected > 0;
            storeBtn.Enabled = highlighted != null;
        }

        async Task UninstallSelected()
        {
            var chosen = sections.Values.SelectMany(x => x).Where(r => r.Checked).Select(r => (AppRow)r.Item).ToList();
            if (chosen.Count == 0) return;
            var critical = chosen.Where(r => r.Level == AppLevel.Critical).ToList();
            var msg = "Uninstall " + chosen.Count + " app(s) for your user account?\n\n" +
                      string.Join("\n", chosen.Select(r => "-  " + r.Name + ": " + r.Effect)) +
                      "\n\nTo get an app back later, click it and choose 'Get from Store'.";
            if (critical.Count > 0)
                msg += "\n\nCRITICAL apps selected: " + string.Join(", ", critical.Select(r => r.Name)) +
                       ". These can break Windows features and may be hard to restore.";
            if (MessageBox.Show(FindForm(), msg, "Uninstall apps", MessageBoxButtons.YesNo,
                    critical.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            uninstallBtn.Enabled = false;
            UseWaitCursor = true;
            foreach (var app in chosen)
            {
                log("Uninstalling " + app.Name + "...");
                var error = await AppScanner.UninstallAsync(app);
                log(error == null ? "REMOVED   " + app.Name : "FAILED    " + app.Name + "  (" + error + ")");
            }
            UseWaitCursor = false;
            Scan();
        }
    }
}
