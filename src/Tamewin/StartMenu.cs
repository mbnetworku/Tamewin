using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Tamewin.Theme;

namespace Tamewin
{
    /// <summary>The look of the Start menu launcher.</summary>
    public enum StartStyle { Glossy, LiquidGlass }

    // ---------------------------------------------------------------------
    // Settings (saved to %LOCALAPPDATA%\Tamewin\startmenu.ini)
    // ---------------------------------------------------------------------

    /// <summary>What style the launcher uses and which right-column shortcuts are hidden.</summary>
    public sealed class StartSettings
    {
        public StartStyle Style = StartStyle.Glossy;
        public readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static string FilePath => Path.Combine(Engine.DataFolder, "startmenu.ini");

        public static StartSettings Load()
        {
            var s = new StartSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var i = line.IndexOf('=');
                    if (i < 0) continue;
                    var key = line.Substring(0, i).Trim();
                    var val = line.Substring(i + 1).Trim();
                    if (key == "style" && val.Equals("liquidglass", StringComparison.OrdinalIgnoreCase)) s.Style = StartStyle.LiquidGlass;
                    else if (key == "hidden") foreach (var h in val.Split(',')) if (h.Length > 0) s.Hidden.Add(h);
                }
            }
            catch { /* use defaults */ }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Engine.DataFolder);
                File.WriteAllLines(FilePath, new[]
                {
                    "# Tamewin Start menu settings",
                    "style=" + (Style == StartStyle.LiquidGlass ? "liquidglass" : "glossy"),
                    "hidden=" + string.Join(",", Hidden),
                });
            }
            catch { /* not fatal */ }
        }
    }

    // ---------------------------------------------------------------------
    // A right-column shortcut (Documents, Computer, Control Panel, ...)
    // ---------------------------------------------------------------------

    public sealed class Shortcut
    {
        public readonly string Key, Label;
        readonly Action open;
        public Shortcut(string key, string label, Action open) { Key = key; Label = label; this.open = open; }
        public void Open() { try { open(); } catch { } }

        static void Explorer(string arg) => Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
        static void Folder(Environment.SpecialFolder f) => Explorer(Environment.GetFolderPath(f));

        /// <summary>The standard Windows 7 right-column list, in order.</summary>
        public static List<Shortcut> Standard() => new List<Shortcut>
        {
            new Shortcut("Documents", "Documents", () => Folder(Environment.SpecialFolder.MyDocuments)),
            new Shortcut("Pictures", "Pictures", () => Folder(Environment.SpecialFolder.MyPictures)),
            new Shortcut("Music", "Music", () => Folder(Environment.SpecialFolder.MyMusic)),
            new Shortcut("Games", "Games", () => Explorer("shell:Games")),
            new Shortcut("Computer", "Computer", () => Explorer("shell:MyComputerFolder")),
            new Shortcut("Control", "Control Panel", () => Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true })),
            new Shortcut("Devices", "Devices and Printers", () => Explorer("shell:::{A8A91A66-3A7D-4424-8D24-04E180695C7A}")),
            new Shortcut("DefaultPrograms", "Default Programs", () => Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.DefaultPrograms") { UseShellExecute = true })),
            new Shortcut("Help", "Help and Support", () => Process.Start(new ProcessStartInfo("https://support.microsoft.com/windows") { UseShellExecute = true })),
        };
    }

    // ---------------------------------------------------------------------
    // Programs found in the Start Menu folders
    // ---------------------------------------------------------------------

    public sealed class ProgramEntry
    {
        public string Name, Path;
        public Image Icon;
    }

    public static class ProgramScanner
    {
        /// <summary>All programs from the user and all-users Start Menu folders, de-duplicated by name.</summary>
        public static List<ProgramEntry> Scan()
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            };
            var found = new Dictionary<string, ProgramEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                var programs = Path.Combine(root ?? "", "Programs");
                if (!Directory.Exists(programs)) continue;
                foreach (var lnk in SafeFind(programs))
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (name.IndexOf("readme", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (!found.ContainsKey(name)) found[name] = new ProgramEntry { Name = name, Path = lnk };
                }
            }
            return found.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static IEnumerable<string> SafeFind(string dir)
        {
            var result = new List<string>();
            try { result.AddRange(Directory.GetFiles(dir, "*.lnk")); } catch { }
            try { foreach (var sub in Directory.GetDirectories(dir)) result.AddRange(SafeFind(sub)); } catch { }
            return result;
        }
    }

    /// <summary>Gets the real icon for a file or shortcut (resolves .lnk targets).</summary>
    static class ShellIcon
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint attrs, ref SHFILEINFO psfi, uint cb, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon; public int iIcon; public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }
        const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0, SHGFI_SMALLICON = 0x1;

        public static Image For(string path, int size)
        {
            var info = new SHFILEINFO();
            var flags = SHGFI_ICON | (size > 20 ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), flags);
            if (info.hIcon == IntPtr.Zero) return null;
            try { using (var ic = Icon.FromHandle(info.hIcon)) return new Bitmap(ic.ToBitmap(), size, size); }
            catch { return null; }
            finally { DestroyIcon(info.hIcon); }
        }
    }

    /// <summary>Real blur-behind (Windows 10/11) via the window composition attribute.</summary>
    static class Glass
    {
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WinCompAttrData data);

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public int AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }
        [StructLayout(LayoutKind.Sequential)]
        struct WinCompAttrData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        public static void Apply(Form f, StartStyle style)
        {
            // Only the liquid-glass style uses a real blur (4 = acrylic). The glossy style is a solid
            // painted Aero gradient, so it stays legible and looks the same everywhere.
            if (style != StartStyle.LiquidGlass) return;
            var accent = new AccentPolicy { AccentFlags = 2, AccentState = 4, GradientColor = 0x5F2A1E14 }; // ABGR: light tint, lets the blur show
            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WinCompAttrData { Attribute = 19 /* WCA_ACCENT_POLICY */, Data = ptr, SizeOfData = size };
                SetWindowCompositionAttribute(f.Handle, ref data);
            }
            catch { /* older Windows: fall back to the painted gradient only */ }
            finally { Marshal.FreeHGlobal(ptr); }
        }
    }

    // ---------------------------------------------------------------------
    // The launcher window
    // ---------------------------------------------------------------------

    /// <summary>A Windows 7-style launcher: real programs on the left, shortcuts on the right,
    /// a working search box and a power button. Opened from Tamewin; it does not replace the real Start button.</summary>
    public sealed class StartMenuForm : Form
    {
        /// <summary>When true the menu stays open if it loses focus (used for screenshots).</summary>
        public bool KeepOpen;

        readonly StartSettings settings;
        readonly List<ProgramEntry> allPrograms;
        readonly GlassPanel progList = new GlassPanel { AutoScroll = true };
        readonly TextBox search = new TextBox();

        public StartMenuForm(StartSettings settings)
        {
            this.settings = settings;
            allPrograms = ProgramScanner.Scan();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(S(440), S(560));
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = settings.Style == StartStyle.LiquidGlass ? Color.FromArgb(20, 28, 40) : Color.FromArgb(222, 236, 250);

            BuildLayout();
            FillPrograms(null);
        }

        bool Light => settings.Style == StartStyle.Glossy;
        Color Ink => Light ? Color.FromArgb(20, 40, 70) : Color.White;
        Color InkDim => Light ? Color.FromArgb(70, 90, 120) : Color.FromArgb(210, 220, 235);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Glass.Apply(this, settings.Style);
            // bottom-left, like the real Start menu
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + S(8), wa.Bottom - Height - S(8));
            search.Focus();
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!KeepOpen) Close(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }

        const int RightColW = 150, PowerH = 44, SearchH = 34;

        void BuildLayout()
        {
            int pad = S(12);
            int rightW = S(RightColW);
            int powerH = S(PowerH);
            int searchH = S(SearchH);

            // Left program list
            progList.Bounds = new Rectangle(pad, pad, Width - rightW - pad * 3, Height - pad * 2 - searchH - powerH);
            progList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(progList);

            // Search box, bottom-left
            search.BorderStyle = BorderStyle.FixedSingle;
            search.Font = Body;
            search.BackColor = Light ? Color.White : Color.FromArgb(40, 52, 68);
            search.ForeColor = Ink;
            search.Bounds = new Rectangle(pad, Height - pad - searchH, Width - rightW - pad * 3, searchH);
            search.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            search.TextChanged += (s, e) => FillPrograms(search.Text);
            search.SetPlaceholder("Search programs");
            Controls.Add(search);

            // Right column of shortcuts
            int rx = Width - rightW - pad;
            int y = pad;
            var head = new Label
            {
                Text = Environment.UserName, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Ink,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(rx, y, rightW, S(40)), BackColor = Color.Transparent,
            };
            Controls.Add(head);
            y += S(48);
            foreach (var sc in Shortcut.Standard())
            {
                if (settings.Hidden.Contains(sc.Key)) continue;
                var link = new StartLink(sc.Label, Ink, InkDim) { Bounds = new Rectangle(rx, y, rightW, S(26)) };
                var captured = sc;
                link.Click += (s, e) => { captured.Open(); Close(); };
                Controls.Add(link);
                y += S(27);
            }

            // Power button, bottom-right
            var power = new StartLink("  Shut down", Ink, InkDim) { Font = new Font("Segoe UI", 10f), Bounds = new Rectangle(rx, Height - pad - powerH, rightW, S(30)) };
            power.Click += (s, e) => PowerMenu();
            Controls.Add(power);
        }

        void FillPrograms(string filter)
        {
            progList.SuspendLayout();
            foreach (Control c in progList.Controls.Cast<Control>().ToList()) c.Dispose();
            progList.Controls.Clear();

            IEnumerable<ProgramEntry> items = allPrograms;
            if (!string.IsNullOrWhiteSpace(filter))
                items = allPrograms.Where(p => p.Name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

            int y = 0, w = progList.ClientSize.Width - S(4);
            foreach (var p in items.Take(200))
            {
                if (p.Icon == null) p.Icon = ShellIcon.For(p.Path, S(24));
                var item = new StartProgram(p, Ink) { Bounds = new Rectangle(0, y, w, S(34)) };
                var captured = p;
                item.Click += (s, e) => { Launch(captured); Close(); };
                progList.Controls.Add(item);
                y += S(34);
            }
            if (progList.Controls.Count == 0)
                progList.Controls.Add(new Label { Text = "No programs found.", ForeColor = InkDim, AutoSize = true, Location = new Point(S(6), S(6)), BackColor = Color.Transparent });
            progList.ResumeLayout();
        }

        static void Launch(ProgramEntry p)
        {
            try { Process.Start(new ProcessStartInfo(p.Path) { UseShellExecute = true }); } catch { }
        }

        void PowerMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Shut down", null, (s, e) => Power("/s"));
            menu.Items.Add("Restart", null, (s, e) => Power("/r"));
            menu.Items.Add("Sign out", null, (s, e) => Power("/l"));
            menu.Show(this, new Point(Width - S(160), Height - S(50)));
        }

        void Power(string flag)
        {
            string what = flag == "/s" ? "shut down" : flag == "/r" ? "restart" : "sign out of";
            if (MessageBox.Show(this, "Are you sure you want to " + what + " this PC?\n\nSave your work first.",
                    "Tamewin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                if (flag == "/l") Process.Start(new ProcessStartInfo("shutdown.exe", "/l") { UseShellExecute = false, CreateNoWindow = true });
                else Process.Start(new ProcessStartInfo("shutdown.exe", flag + " /t 0") { UseShellExecute = false, CreateNoWindow = true });
            }
            catch { }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            PaintBgRect(e.Graphics, ClientRectangle);
            using (var pen = new Pen(Light ? Color.FromArgb(150, 160, 185) : Color.FromArgb(70, 160, 180, 210)))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientRectangle.Width - 1, ClientRectangle.Height - 1);
        }

        /// <summary>The window's gradient over the whole client rect. Children call <see cref="PaintBg"/>
        /// so they draw the matching slice under themselves instead of a black box.</summary>
        void PaintBgRect(Graphics g, Rectangle r)
        {
            if (Light)
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(235, 244, 252), Color.FromArgb(200, 220, 242), 90f))
                    g.FillRectangle(b, r);
                var top = new Rectangle(r.X, r.Y, r.Width, r.Height / 3);
                using (var b = new LinearGradientBrush(top, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(10, 255, 255, 255), 90f))
                    g.FillRectangle(b, top);
            }
            else
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(234, 34, 44, 64), Color.FromArgb(234, 18, 25, 38), 90f))
                    g.FillRectangle(b, r);
                var top = new Rectangle(r.X, r.Y, r.Width, r.Height / 3);
                using (var b = new LinearGradientBrush(top, Color.FromArgb(40, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(b, top);
            }
        }

        /// <summary>Paints the window gradient slice that sits under <paramref name="child"/>.</summary>
        public void PaintBg(Graphics g, Control child)
        {
            var origin = PointToClient(child.PointToScreen(Point.Empty));
            var state = g.Save();
            g.TranslateTransform(-origin.X, -origin.Y);
            PaintBgRect(g, ClientRectangle);
            g.Restore(state);
        }
    }

    /// <summary>A scrolling panel that paints the Start menu's gradient under itself.</summary>
    sealed class GlassPanel : Panel
    {
        public GlassPanel() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (FindForm() is StartMenuForm m) m.PaintBg(e.Graphics, this);
            else base.OnPaintBackground(e);
        }
    }

    /// <summary>A program row in the launcher: icon + name, with hover.</summary>
    sealed class StartProgram : PaintedControl
    {
        readonly ProgramEntry prog;
        readonly Color ink;

        public StartProgram(ProgramEntry prog, Color ink)
        {
            this.prog = prog; this.ink = ink;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this); else g.Clear(BackColor);
            if (Hover)
                using (var b = new SolidBrush(Color.FromArgb(90, 120, 170, 230)))
                using (var path = Round(new Rectangle(0, 0, Width - 1, Height - 1), S(4)))
                    g.FillPath(b, path);
            int ic = S(24), pad = S(6);
            if (prog.Icon != null) g.DrawImage(prog.Icon, pad, (Height - ic) / 2, ic, ic);
            var textR = new Rectangle(pad + ic + S(8), 0, Width - pad - ic - S(10), Height);
            TextRenderer.DrawText(g, prog.Name, Theme.Body, textR, ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>A right-column text link (shortcut or power), underlined on hover.</summary>
    sealed class StartLink : PaintedControl
    {
        readonly Color ink, dim;
        public StartLink(string text, Color ink, Color dim) { this.ink = ink; this.dim = dim; Text = text; Cursor = Cursors.Hand; Font = Theme.Body; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this); else g.Clear(BackColor);
            var r = new Rectangle(S(4), 0, Width - S(6), Height);
            using (var f = new Font(Font, Hover ? FontStyle.Underline : FontStyle.Regular))
                TextRenderer.DrawText(g, Text, f, r, Hover ? ink : dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }
}
