using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;
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

    /// <summary>A Start-menu entry: either a program (a .lnk) or a folder with children (like Windows 7's groups).</summary>
    public sealed class ProgramEntry
    {
        public string Name, Path;
        public Image Icon;
        public bool IsFolder;
        public List<ProgramEntry> Children;   // set when IsFolder
    }

    public static class ProgramScanner
    {
        /// <summary>The top level of the Start Menu (programs and folders), like the "All Programs" view in Windows 7.
        /// Folders keep their children so they can open as a submenu. The user and all-users menus are merged.</summary>
        public static List<ProgramEntry> Scan()
        {
            var roots = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) ?? "", "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) ?? "", "Programs"),
            };
            var merged = new Dictionary<string, ProgramEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
                foreach (var node in ReadFolder(root))
                    Merge(merged, node);
            return Sort(merged.Values);
        }

        /// <summary>Flat list of every program (used for search), with folders expanded.</summary>
        public static List<ProgramEntry> Flatten(List<ProgramEntry> nodes)
        {
            var list = new List<ProgramEntry>();
            void Walk(List<ProgramEntry> ns) { foreach (var n in ns) { if (n.IsFolder) Walk(n.Children); else list.Add(n); } }
            Walk(nodes);
            return list.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                       .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static List<ProgramEntry> ReadFolder(string dir)
        {
            var result = new List<ProgramEntry>();
            try
            {
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var children = ReadFolder(sub);
                    if (children.Count > 0)
                        result.Add(new ProgramEntry { Name = Path.GetFileName(sub), Path = sub, IsFolder = true, Children = children });
                }
                foreach (var lnk in Directory.GetFiles(dir, "*.lnk"))
                {
                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (name.IndexOf("readme", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    result.Add(new ProgramEntry { Name = name, Path = lnk });
                }
            }
            catch { }
            return result;
        }

        static void Merge(Dictionary<string, ProgramEntry> into, ProgramEntry node)
        {
            if (!into.TryGetValue(node.Name, out var existing)) { into[node.Name] = node; return; }
            if (existing.IsFolder && node.IsFolder)
            {
                var byName = existing.Children.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var c in node.Children) Merge(byName, c);
                existing.Children = Sort(byName.Values);
            }
        }

        // Windows 7 order: folders first, then programs, each alphabetical.
        static List<ProgramEntry> Sort(IEnumerable<ProgramEntry> items) =>
            items.OrderByDescending(p => p.IsFolder).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Loads the user's real Windows account picture, so the launcher shows the same photo as the real menu.</summary>
    static class AccountPicture
    {
        public static Image Load(int size)
        {
            // Registry: HKLM\...\AccountPicture\Users\<SID> holds paths to the user's picture at several sizes.
            try
            {
                var sid = WindowsIdentity.GetCurrent().User?.Value;
                if (sid != null)
                    using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid))
                        if (k != null)
                            foreach (var name in new[] { "Image448", "Image192", "Image96", "Image64", "Image48", "Image40", "Image32" })
                                if (k.GetValue(name) is string path && File.Exists(path))
                                    return Rounded(path, size);
            }
            catch { }
            // Fallback: the generic default account picture.
            try
            {
                var def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\User Account Pictures\user.png");
                if (File.Exists(def)) return Rounded(def, size);
            }
            catch { }
            return null;
        }

        static Image Rounded(string path, int size)
        {
            using (var src = Image.FromFile(path))
            {
                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var clip = Theme.Round(new Rectangle(0, 0, size, size), size / 6))
                    {
                        g.SetClip(clip);
                        g.DrawImage(src, new Rectangle(0, 0, size, size));
                    }
                }
                return bmp;
            }
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
        readonly List<ProgramEntry> topNodes;    // folders + programs, like Windows 7's All Programs
        readonly List<ProgramEntry> allFlat;     // every program, for search
        readonly GlassPanel progList = new GlassPanel { AutoScroll = true };
        readonly TextBox search = new TextBox();

        // Windows 7 colours: white left pane, dark glass right column.
        static readonly Color LeftTop = Color.FromArgb(255, 255, 255), LeftBottom = Color.FromArgb(244, 248, 252);
        static readonly Color ProgInk = Color.FromArgb(28, 42, 64);
        static readonly Color RightInk = Color.FromArgb(238, 243, 250), RightDim = Color.FromArgb(205, 216, 232);

        public StartMenuForm(StartSettings settings)
        {
            this.settings = settings;
            topNodes = ProgramScanner.Scan();
            allFlat = ProgramScanner.Flatten(topNodes);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(S(392), S(500));
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 32, 46);

            BuildLayout();
            FillPrograms(null);
        }

        int Frame => S(6);
        int RightW => S(150);
        int RightX => Width - RightW;
        Rectangle LeftPane => Rectangle.FromLTRB(Frame, Frame, RightX, Height - Frame);

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Region = new Region(Round(new Rectangle(0, 0, Width, Height), S(9)));
            Glass.Apply(this, settings.Style);
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + S(8), wa.Bottom - Height - S(8));
            search.Focus();
        }

        bool menuOpen;         // true while a submenu / power menu is showing, so the launcher doesn't close under it
        bool allProgramsView;  // false = flat program list (default), true = the folder tree ("All Programs")
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!KeepOpen && !menuOpen) Close(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }

        void BuildLayout()
        {
            var lp = LeftPane;
            int searchH = S(30), allProgH = S(30), gap = S(2);

            // Left: program list, then an "All Programs" row and a search box at the bottom.
            progList.Bounds = new Rectangle(lp.Left + S(3), lp.Top + S(3), lp.Width - S(6), lp.Height - S(6) - allProgH - searchH - gap * 2);
            Controls.Add(progList);

            var allProg = new StartAllPrograms { Bounds = new Rectangle(lp.Left + S(3), progList.Bottom + gap, lp.Width - S(6), allProgH) };
            allProg.Click += (s, e) =>
            {
                allProgramsView = !allProgramsView;
                allProg.Back = allProgramsView;
                allProg.Invalidate();
                search.Clear();
                FillPrograms(null);
            };
            Controls.Add(allProg);

            search.BorderStyle = BorderStyle.FixedSingle;
            search.Font = Body;
            search.BackColor = Color.White;
            search.ForeColor = ProgInk;
            search.Bounds = new Rectangle(lp.Left + S(3), lp.Bottom - searchH - S(3), lp.Width - S(6), searchH);
            search.TextChanged += (s, e) => FillPrograms(search.Text);
            search.SetPlaceholder("Search programs and files");
            Controls.Add(search);

            // Right: avatar, user name, shortcuts, and a Shut down button.
            int rx = RightX + S(8), rw = RightW - S(14);
            int av = S(46), ax = RightX + (RightW - av) / 2;
            Controls.Add(new StartAvatar(AccountPicture.Load(av), Environment.UserName) { Bounds = new Rectangle(ax, Frame + S(8), av, av) });
            var name = new Label
            {
                Text = Environment.UserName, Font = new Font("Segoe UI Semibold", 10f), ForeColor = RightInk,
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent,
                Bounds = new Rectangle(RightX + S(2), Frame + S(56), RightW - S(4), S(20)),
            };
            Controls.Add(name);

            var linkFont = new Font("Segoe UI", 8.5f);
            int y = Frame + S(84);
            foreach (var sc in Shortcut.Standard())
            {
                if (settings.Hidden.Contains(sc.Key)) continue;
                var link = new StartLink(sc.Label, RightInk, RightDim) { Font = linkFont, Bounds = new Rectangle(rx, y, rw, S(24)) };
                var captured = sc;
                link.Click += (s, e) => { captured.Open(); Close(); };
                Controls.Add(link);
                y += S(25);
            }

            var power = new StartPower { Bounds = new Rectangle(rx, Height - Frame - S(36), rw, S(28)) };
            power.ShutDown += (s, e) => Power("/s");
            power.MoreOptions += (s, e) => PowerMenu();
            Controls.Add(power);
        }

        void FillPrograms(string filter)
        {
            progList.SuspendLayout();
            foreach (Control c in progList.Controls.Cast<Control>().ToList()) c.Dispose();
            progList.Controls.Clear();

            bool searching = !string.IsNullOrWhiteSpace(filter);
            IEnumerable<ProgramEntry> items =
                searching ? allFlat.Where(p => p.Name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) :
                allProgramsView ? (IEnumerable<ProgramEntry>)topNodes :
                allFlat;

            int y = S(2), w = progList.ClientSize.Width - S(4);
            foreach (var p in items.Take(300))
            {
                if (p.Icon == null) p.Icon = ShellIcon.For(p.Path, S(20));
                var item = new StartProgram(p, ProgInk) { Bounds = new Rectangle(S(2), y, w, S(29)) };
                var captured = p;
                if (p.IsFolder) item.Click += (s, e) => OpenFolder(captured, item);
                else item.Click += (s, e) => { Launch(captured); Close(); };
                progList.Controls.Add(item);
                y += S(29);
            }
            if (progList.Controls.Count == 0)
                progList.Controls.Add(new Label { Text = "No programs found.", ForeColor = Color.Gray, AutoSize = true, Location = new Point(S(8), S(8)), BackColor = Color.Transparent });
            progList.ResumeLayout();
        }

        static void Launch(ProgramEntry p)
        {
            try { Process.Start(new ProcessStartInfo(p.Path) { UseShellExecute = true }); } catch { }
        }

        /// <summary>Opens a folder's contents as a cascading submenu to the right, like Windows 7.</summary>
        void OpenFolder(ProgramEntry folder, Control anchor)
        {
            BuildSubmenu(folder.Children).Show(anchor, new Point(anchor.Width - S(6), 0));
        }

        ContextMenuStrip BuildSubmenu(List<ProgramEntry> nodes)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true };
            menu.Opened += (s, e) => menuOpen = true;
            menu.Closed += (s, e) => { menuOpen = false; if (!KeepOpen && !ContainsFocus) Close(); };
            foreach (var n in nodes)
            {
                if (n.Icon == null) n.Icon = ShellIcon.For(n.Path, S(16));
                if (n.IsFolder)
                {
                    var item = new ToolStripMenuItem(n.Name, n.Icon);
                    foreach (ToolStripItem child in BuildSubmenu(n.Children).Items.Cast<ToolStripItem>().ToArray())
                        item.DropDownItems.Add(child);
                    menu.Items.Add(item);
                }
                else
                {
                    var captured = n;
                    var item = new ToolStripMenuItem(n.Name, n.Icon, (s, e) => { Launch(captured); Close(); });
                    menu.Items.Add(item);
                }
            }
            return menu;
        }

        void PowerMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Opened += (s, e) => menuOpen = true;
            menu.Closed += (s, e) => menuOpen = false;
            menu.Items.Add("Shut down", null, (s, e) => Power("/s"));
            menu.Items.Add("Restart", null, (s, e) => Power("/r"));
            menu.Items.Add("Sign out", null, (s, e) => Power("/l"));
            menu.Show(this, new Point(RightX, Height - Frame - S(12)));
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
            using (var pen = new Pen(Color.FromArgb(120, 150, 185)))
            using (var path = Round(new Rectangle(0, 0, ClientRectangle.Width - 1, ClientRectangle.Height - 1), S(9)))
                e.Graphics.DrawPath(pen, path);
        }

        /// <summary>Paints the whole window: dark glass frame + right column, white left pane.
        /// Children call <see cref="PaintBg"/> so they draw the matching slice under themselves.</summary>
        void PaintBgRect(Graphics g, Rectangle r)
        {
            // Dark glass frame / right column
            if (settings.Style == StartStyle.LiquidGlass)
                using (var b = new SolidBrush(Color.FromArgb(150, 20, 28, 42))) g.FillRectangle(b, r);   // translucent: real blur shows behind
            else
                using (var b = new LinearGradientBrush(r, Color.FromArgb(46, 58, 80), Color.FromArgb(22, 30, 46), 90f)) g.FillRectangle(b, r);
            // top sheen on the dark glass
            var sheen = new Rectangle(r.X, r.Y, r.Width, r.Height / 4);
            using (var b = new LinearGradientBrush(sheen, Color.FromArgb(45, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                g.FillRectangle(b, sheen);
            // White left pane (always opaque, for readable program names)
            var lp = LeftPane;
            using (var b = new LinearGradientBrush(lp, LeftTop, LeftBottom, 90f)) g.FillRectangle(b, lp);
            using (var pen = new Pen(Color.FromArgb(180, 196, 214))) g.DrawRectangle(pen, lp);
        }

        /// <summary>Paints the slice of the window that sits under <paramref name="child"/>.</summary>
        public void PaintBg(Graphics g, Control child)
        {
            var origin = PointToClient(child.PointToScreen(Point.Empty));
            var state = g.Save();
            g.TranslateTransform(-origin.X, -origin.Y);
            PaintBgRect(g, ClientRectangle);
            g.Restore(state);
        }
    }

    /// <summary>A scrolling panel that paints the Start menu background under itself.</summary>
    sealed class GlassPanel : Panel
    {
        public GlassPanel() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (FindForm() is StartMenuForm m) m.PaintBg(e.Graphics, this);
            else base.OnPaintBackground(e);
        }
    }

    /// <summary>A program row: icon + name, with a soft hover highlight. Lives on the white left pane.</summary>
    sealed class StartProgram : PaintedControl
    {
        readonly ProgramEntry prog;
        readonly Color ink;

        public StartProgram(ProgramEntry prog, Color ink) { this.prog = prog; this.ink = ink; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this); else g.Clear(BackColor);
            if (Hover)
            {
                var rr = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var b = new LinearGradientBrush(rr, Color.FromArgb(230, 240, 252), Color.FromArgb(205, 224, 246), 90f))
                using (var path = Round(rr, S(4)))
                    g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(160, 196, 230)))
                using (var path = Round(rr, S(4)))
                    g.DrawPath(pen, path);
            }
            int ic = S(20), pad = S(6);
            if (prog.Icon != null) g.DrawImage(prog.Icon, pad, (Height - ic) / 2, ic, ic);
            int rightPad = prog.IsFolder ? S(18) : S(10);
            var textR = new Rectangle(pad + ic + S(8), 0, Width - pad - ic - S(8) - rightPad, Height);
            TextRenderer.DrawText(g, prog.Name, Theme.Body, textR, ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (prog.IsFolder)
            {
                var arrowR = new Rectangle(Width - S(16), 0, S(14), Height);
                TextRenderer.DrawText(g, "▸", new Font("Segoe UI", 9f), arrowR, Color.FromArgb(70, 85, 110),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>The "All Programs" row at the bottom of the left pane, with a divider above it.</summary>
    sealed class StartAllPrograms : PaintedControl
    {
        public bool Back;   // false: "All Programs" (▶); true: "Back" (◀)
        public StartAllPrograms() { Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this); else g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(205, 215, 228))) g.DrawLine(pen, S(2), 0, Width - S(2), 0);
            if (Hover)
                using (var b = new SolidBrush(Color.FromArgb(220, 232, 246)))
                using (var path = Round(new Rectangle(0, S(2), Width - 1, Height - S(3)), S(4)))
                    g.FillPath(b, path);
            var box = new Rectangle(S(6), (Height - S(16)) / 2, S(16), S(16));
            using (var b = new LinearGradientBrush(box, Color.FromArgb(120, 170, 90), Color.FromArgb(70, 120, 50), 90f))
                g.FillRectangle(b, box);
            TextRenderer.DrawText(g, Back ? "◀" : "▶", new Font("Segoe UI", 7f), box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            var tr = new Rectangle(S(28), 0, Width - S(30), Height);
            TextRenderer.DrawText(g, Back ? "Back" : "All Programs", Theme.BodyBold, tr, Color.FromArgb(28, 42, 64),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>The user picture tile: a rounded gradient square with the user's initial.</summary>
    sealed class StartAvatar : PaintedControl
    {
        readonly Image photo;
        readonly string initial;
        public StartAvatar(Image photo, string user)
        {
            this.photo = photo;
            initial = string.IsNullOrEmpty(user) ? "?" : user.Substring(0, 1).ToUpperInvariant();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (photo != null) g.DrawImage(photo, 0, 0, Width, Height);
            else
                using (var b = new LinearGradientBrush(r, Color.FromArgb(246, 170, 90), Color.FromArgb(206, 96, 60), 45f))
                using (var path = Round(r, S(8)))
                {
                    g.FillPath(b, path);
                    TextRenderer.DrawText(g, initial, new Font("Segoe UI Semibold", 18f), r, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            // white beveled frame, like the Windows 7 picture tile
            using (var pen = new Pen(Color.White, S(2)))
            using (var path = Round(r, S(8)))
                g.DrawPath(pen, path);
            using (var pen = new Pen(Color.FromArgb(120, 120, 130)))
            using (var path = Round(new Rectangle(0, 0, Width - 1, Height - 1), S(8)))
                g.DrawPath(pen, path);
        }
    }

    /// <summary>Windows 7-style Shut down button: a beveled button with a split "more options" arrow.</summary>
    sealed class StartPower : PaintedControl
    {
        public event EventHandler ShutDown, MoreOptions;
        public StartPower() { Cursor = Cursors.Hand; }
        int ArrowW => S(22);

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (e.X >= Width - ArrowW) MoreOptions?.Invoke(this, EventArgs.Empty);
                else ShutDown?.Invoke(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (FindForm() is StartMenuForm m) m.PaintBg(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(252, 252, 252), Color.FromArgb(222, 226, 232), 90f))
            using (var path = Round(r, S(4)))
                g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(120, 130, 146)))
            using (var path = Round(r, S(4)))
                g.DrawPath(pen, path);
            var textR = new Rectangle(0, 0, Width - ArrowW, Height);
            TextRenderer.DrawText(g, "Shut down", Theme.Body, textR, Color.FromArgb(30, 40, 56),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (var pen = new Pen(Color.FromArgb(150, 158, 172))) g.DrawLine(pen, Width - ArrowW, S(3), Width - ArrowW, Height - S(4));
            var arrowR = new Rectangle(Width - ArrowW, 0, ArrowW, Height);
            TextRenderer.DrawText(g, "▸", new Font("Segoe UI", 9f), arrowR, Color.FromArgb(40, 50, 66),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Hover)
            {
                var rr = new Rectangle(0, S(1), Width - 1, Height - S(2));
                using (var b = new LinearGradientBrush(rr, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(24, 255, 255, 255), 90f))
                using (var path = Round(rr, S(4)))
                    g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255)))
                using (var path = Round(rr, S(4)))
                    g.DrawPath(pen, path);
            }
            var r = new Rectangle(S(8), 0, Width - S(10), Height);
            TextRenderer.DrawText(g, Text, Font, r, Hover ? ink : dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }
}
