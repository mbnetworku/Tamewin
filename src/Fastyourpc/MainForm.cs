using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fastyourpc
{
    public sealed class MainForm : Form
    {
        readonly Engine engine;
        readonly ListView tweakList = NewList();
        readonly ListView appList = NewList();
        readonly Label tweakInfo = NewInfo();
        readonly Label appInfo = NewInfo();
        readonly TextBox log = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f), BackColor = SystemColors.Window,
        };
        bool busy;

        public MainForm(Engine engine)
        {
            this.engine = engine;
            Text = "Fastyourpc " + Application.ProductVersion + (engine.TestMode ? "   [TEST MODE]" : "");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(980, 760);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildTweakTab());
            tabs.TabPages.Add(BuildAppTab());
            tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedIndex == 1 && appList.Items.Count == 0) ScanApps(); };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            layout.Controls.Add(BuildAdminBar(), 0, 0);
            layout.Controls.Add(tabs, 0, 1);
            layout.Controls.Add(log, 0, 2);
            Controls.Add(layout);

            Log("Fastyourpc started. Nothing is changed until you click Apply or Uninstall.");
            Log("Backups are kept in " + Engine.DataFolder);
            RefreshTweaks();
        }

        // ================= common UI =================

        static ListView NewList() => new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true,
            ShowItemToolTips = true, HideSelection = false, ShowGroups = true,
        };

        static Label NewInfo() => new Label
        {
            Dock = DockStyle.Fill, AutoSize = false, Height = 48, Padding = new Padding(4),
            Text = "Click an item to see what it does.",
        };

        static Button Btn(string text, EventHandler onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3), Padding = new Padding(6, 2, 6, 2) };
            b.Click += onClick;
            return b;
        }

        static TabPage Tab(string title, Control list, Control info, params Control[] buttons)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            bar.Controls.AddRange(buttons);
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(list, 0, 0);
            t.Controls.Add(info, 0, 1);
            t.Controls.Add(bar, 0, 2);
            var page = new TabPage(title);
            page.Controls.Add(t);
            return page;
        }

        Control BuildAdminBar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4) };
            var label = new Label { AutoSize = true, Margin = new Padding(3, 8, 12, 3) };
            if (Engine.IsAdmin)
            {
                label.Text = "Running as Administrator: every option is available.";
                bar.Controls.Add(label);
            }
            else
            {
                label.Text = "Running as a normal user: options marked 'Administrator' are locked (grey).";
                bar.Controls.Add(label);
                bar.Controls.Add(Btn("Restart as Administrator", (s, e) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" });
                        Close();
                    }
                    catch { Log("Administrator restart was cancelled."); }
                }));
            }
            return bar;
        }

        void Log(string line)
        {
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
        }

        static void SetChecked(ListView list, Func<ListViewItem, bool> pick)
        {
            foreach (ListViewItem i in list.Items) i.Checked = pick(i);
        }

        // ================= Privacy & settings tab =================

        TabPage BuildTweakTab()
        {
            tweakList.Columns.Add("Setting", 520);
            tweakList.Columns.Add("Status", 110);
            tweakList.Columns.Add("Needs", 120);
            foreach (var g in Catalog.Groups) tweakList.Groups.Add(g, g);
            foreach (var t in Catalog.All)
            {
                var item = new ListViewItem(t.Title) { Tag = t, Group = tweakList.Groups[t.Group], ToolTipText = t.Info };
                item.SubItems.Add("");
                item.SubItems.Add(t.Admin ? "Administrator" : "");
                tweakList.Items.Add(item);
            }
            tweakList.ItemCheck += (s, e) =>
            {
                var t = (Tweak)tweakList.Items[e.Index].Tag;
                if (!engine.CanRun(t)) e.NewValue = CheckState.Unchecked;
            };
            tweakList.SelectedIndexChanged += (s, e) =>
            {
                if (tweakList.SelectedItems.Count == 0) return;
                var t = (Tweak)tweakList.SelectedItems[0].Tag;
                tweakInfo.Text = t.Info + (t.Warning != null ? "  WARNING: " + t.Warning : "") +
                                 (t.Note != null ? "  (" + t.Note + ")" : "");
            };

            return Tab("Privacy & settings", tweakList, tweakInfo,
                Btn("Apply selected", (s, e) => RunTweaks(apply: true)),
                Btn("Revert selected", (s, e) => RunTweaks(apply: false)),
                Btn("Select recommended", (s, e) => SetChecked(tweakList, i => !((Tweak)i.Tag).OptIn && engine.CanRun((Tweak)i.Tag))),
                Btn("Select applied", (s, e) => SetChecked(tweakList, i => engine.State((Tweak)i.Tag) != TweakState.NotApplied && engine.CanRun((Tweak)i.Tag))),
                Btn("Select none", (s, e) => SetChecked(tweakList, i => false)),
                Btn("Refresh", (s, e) => RefreshTweaks()),
                Btn("Open printer settings", (s, e) => Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true })),
                Btn("Restart Explorer", (s, e) => RestartExplorer()));
        }

        void RefreshTweaks()
        {
            foreach (ListViewItem item in tweakList.Items)
            {
                var t = (Tweak)item.Tag;
                var state = engine.State(t);
                item.SubItems[1].Text = state == TweakState.Applied ? "Applied" : state == TweakState.Partly ? "Partly" : "Not applied";
                item.ForeColor = engine.CanRun(t) ? SystemColors.WindowText : SystemColors.GrayText;
            }
        }

        void RunTweaks(bool apply)
        {
            var chosen = tweakList.CheckedItems.Cast<ListViewItem>().Select(i => (Tweak)i.Tag).ToList();
            if (chosen.Count == 0) { MessageBox.Show(this, "Tick at least one setting first.", Text); return; }

            var verb = apply ? "Apply" : "Revert";
            var msg = verb + " " + chosen.Count + " setting(s)?";
            var warnings = apply ? chosen.Where(t => t.Warning != null).Select(t => "- " + t.Title + ": " + t.Warning).ToList() : new List<string>();
            if (warnings.Count > 0) msg += "\n\nWARNING:\n" + string.Join("\n", warnings);
            if (apply && chosen.Any(t => t.Id == "wpp_on") && chosen.Any(t => t.Id == "wpp_off"))
            {
                MessageBox.Show(this, "Choose either protected print ON or OFF, not both.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(this, msg, Text, MessageBoxButtons.YesNo,
                    warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (var t in chosen)
                if (apply) engine.Apply(t, Log); else engine.Revert(t, Log);
            RefreshTweaks();
            SetChecked(tweakList, i => false);

            var notes = chosen.Where(t => t.Note != null).Select(t => t.Note).Distinct().ToList();
            Log(verb + " finished." + (notes.Count > 0 ? " " + string.Join(". ", notes) + "." : ""));
            if (notes.Count > 0) MessageBox.Show(this, "Done.\n\n" + string.Join("\n", notes), Text);
        }

        void RestartExplorer()
        {
            if (MessageBox.Show(this, "Restart Explorer now? Open File Explorer windows will close.", Text,
                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
            System.Threading.Thread.Sleep(1500);
            if (Process.GetProcessesByName("explorer").Length == 0) Process.Start("explorer.exe");
            Log("Explorer restarted.");
        }

        // ================= Apps (bloatware) tab =================

        TabPage BuildAppTab()
        {
            appList.Columns.Add("App", 300);
            appList.Columns.Add("Status", 100);
            appList.Columns.Add("If you remove it", 520);
            foreach (AppLevel l in Enum.GetValues(typeof(AppLevel)))
                appList.Groups.Add(l.ToString(), AppCatalog.LevelTitle(l));

            appList.ItemCheck += (s, e) =>
            {
                if (e.NewValue != CheckState.Checked) return;
                var row = (AppRow)appList.Items[e.Index].Tag;
                if (!row.Installed) { e.NewValue = CheckState.Unchecked; return; }
                if (row.Level == AppLevel.Critical && !busy &&
                    MessageBox.Show(this,
                        row.Name + " is CRITICAL.\n\n" + row.Effect +
                        "\n\nRemoving it is NOT recommended. Select it anyway?",
                        "Critical app", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    e.NewValue = CheckState.Unchecked;
            };
            appList.SelectedIndexChanged += (s, e) =>
            {
                if (appList.SelectedItems.Count == 0) return;
                var row = (AppRow)appList.SelectedItems[0].Tag;
                appInfo.Text = row.Name + " - " + (row.Installed ? "installed" : "not installed") + ". " +
                               AppCatalog.LevelTitle(row.Level) + ". If you remove it: " + row.Effect +
                               (row.Installed ? "  Packages: " + string.Join(", ", row.Packages.Select(p => p.Id.Name)) : "");
            };

            return Tab("Apps (bloatware)", appList, appInfo,
                Btn("Scan again", (s, e) => ScanApps()),
                Btn("Uninstall selected", async (s, e) => await UninstallSelected()),
                Btn("Select all safe", (s, e) => SetChecked(appList, i => ((AppRow)i.Tag).Installed && ((AppRow)i.Tag).Level == AppLevel.Safe)),
                Btn("Select none", (s, e) => SetChecked(appList, i => false)),
                Btn("Get highlighted app from Store", (s, e) =>
                {
                    if (appList.SelectedItems.Count == 0) { MessageBox.Show(this, "Click an app in the list first.", Text); return; }
                    AppScanner.OpenInStore((AppRow)appList.SelectedItems[0].Tag);
                }));
        }

        void ScanApps()
        {
            Log("Scanning installed apps...");
            List<AppRow> rows;
            try { rows = AppScanner.Scan(); }
            catch (Exception ex) { Log("Scan failed: " + ex.Message); return; }

            busy = true;
            appList.BeginUpdate();
            appList.Items.Clear();
            foreach (var row in rows.OrderBy(r => r.Level).ThenBy(r => !r.Installed).ThenBy(r => r.Name))
            {
                var item = new ListViewItem(row.Name) { Tag = row, Group = appList.Groups[row.Level.ToString()], ToolTipText = row.Effect };
                item.SubItems.Add(row.Installed ? "Installed" : "Not installed");
                item.SubItems.Add(row.Effect);
                if (!row.Installed) item.ForeColor = SystemColors.GrayText;
                else if (row.Level == AppLevel.Critical) item.ForeColor = Color.Firebrick;
                appList.Items.Add(item);
            }
            appList.EndUpdate();
            busy = false;

            var known = rows.Where(r => r.Level != AppLevel.Other).ToList();
            Log("Scan done: " + known.Count(r => r.Installed) + " of " + known.Count + " known apps installed (" +
                known.Count(r => r.Installed && r.Level == AppLevel.Safe) + " safe to remove), " +
                rows.Count(r => r.Level == AppLevel.Other) + " other apps.");
        }

        async Task UninstallSelected()
        {
            var chosen = appList.CheckedItems.Cast<ListViewItem>().Select(i => (AppRow)i.Tag).Where(r => r.Installed).ToList();
            if (chosen.Count == 0) { MessageBox.Show(this, "Tick at least one installed app first.", Text); return; }

            var critical = chosen.Where(r => r.Level == AppLevel.Critical).ToList();
            var msg = "Uninstall " + chosen.Count + " app(s) for your user account?\n\n" +
                      string.Join("\n", chosen.Select(r => "- " + r.Name + ": " + r.Effect)) +
                      "\n\nTo get an app back later, use 'Get highlighted app from Store'.";
            if (critical.Count > 0)
                msg += "\n\nCRITICAL apps selected: " + string.Join(", ", critical.Select(r => r.Name)) +
                       ". These can break Windows features and may be hard to restore.";
            if (MessageBox.Show(this, msg, "Uninstall apps", MessageBoxButtons.YesNo,
                    critical.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            UseWaitCursor = true;
            foreach (var row in chosen)
            {
                Log("Uninstalling " + row.Name + "...");
                var error = await AppScanner.UninstallAsync(row);
                Log(error == null ? "REMOVED   " + row.Name : "FAILED    " + row.Name + "  (" + error + ")");
            }
            UseWaitCursor = false;
            ScanApps();
        }
    }
}
