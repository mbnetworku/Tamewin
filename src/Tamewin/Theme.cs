using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tamewin
{
    /// <summary>A small coloured label drawn on a row, e.g. "Applied" or "Critical".</summary>
    public struct Pill
    {
        public string Text;
        public Color Fore, Back;
        public Pill(string text, Color fore, Color back) { Text = text; Fore = fore; Back = back; }
    }

    /// <summary>Colours, fonts and sizes, in the style of Windows 11 Settings. Follows the Windows light/dark setting.</summary>
    public static class Theme
    {
        public static bool Dark { get; private set; }
        public static float Scale { get; private set; } = 1f;

        public static Color Window, Sidebar, Card, CardHover, Selected, Border, Text, TextDim, TextDisabled,
                            Accent, AccentHover, OnAccent;
        static Color greenF, greenB, amberF, amberB, redF, redB, grayF, grayB, blueF, blueB;

        public static Font Body, BodyBold, Desc, Small, SmallBold, Section, PageTitle, AppTitle, Icons;

        /// <summary>Call once at startup. forceDark: null = follow Windows.</summary>
        public static void Init(bool? forceDark = null)
        {
            Dark = forceDark ?? WindowsUsesDarkApps();
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;

            Window       = Dark ? Hex(0x202020) : Hex(0xF3F3F3);
            Sidebar      = Dark ? Hex(0x1B1B1B) : Hex(0xEBEBEB);
            Card         = Dark ? Hex(0x2B2B2B) : Hex(0xFFFFFF);
            CardHover    = Dark ? Hex(0x323232) : Hex(0xF8F8F8);
            Selected     = Dark ? Hex(0x2D2D2D) : Hex(0xFFFFFF);
            Border       = Dark ? Hex(0x3A3A3A) : Hex(0xE2E2E2);
            Text         = Dark ? Hex(0xFFFFFF) : Hex(0x1B1B1B);
            TextDim      = Dark ? Hex(0xC2C2C2) : Hex(0x5D5D5D);
            TextDisabled = Dark ? Hex(0x787878) : Hex(0xA3A3A3);
            Accent       = Dark ? Hex(0x60CDFF) : Hex(0x005FB8);
            AccentHover  = Dark ? Hex(0x7DD7FF) : Hex(0x1A6EC2);
            OnAccent     = Dark ? Hex(0x00243A) : Hex(0xFFFFFF);

            greenF = Dark ? Hex(0x6CCB5F) : Hex(0x0F7B0F); greenB = Dark ? Hex(0x223A21) : Hex(0xDFF6DD);
            amberF = Dark ? Hex(0xFCE100) : Hex(0x8A5300); amberB = Dark ? Hex(0x433519) : Hex(0xFFF4CE);
            redF   = Dark ? Hex(0xFF99A4) : Hex(0xC42B1C); redB   = Dark ? Hex(0x442726) : Hex(0xFDE7E9);
            grayF  = Dark ? Hex(0xC2C2C2) : Hex(0x5D5D5D); grayB  = Dark ? Hex(0x3A3A3A) : Hex(0xECECEC);
            blueF  = Dark ? Hex(0x60CDFF) : Hex(0x005FB8); blueB  = Dark ? Hex(0x1D3446) : Hex(0xE1EFFA);

            Body      = new Font("Segoe UI", 10f);
            BodyBold  = new Font("Segoe UI Semibold", 10f);
            Desc      = new Font("Segoe UI", 9f);
            Small     = new Font("Segoe UI", 8.25f);
            SmallBold = new Font("Segoe UI Semibold", 8.25f);
            Section   = new Font("Segoe UI Semibold", 11.5f);
            PageTitle = new Font("Segoe UI Semibold", 20f);
            AppTitle  = new Font("Segoe UI Semibold", 13f);
            Icons     = IconFont(12f);
        }

        public static Pill Green(string t) => new Pill(t, greenF, greenB);
        public static Pill Amber(string t) => new Pill(t, amberF, amberB);
        public static Pill Red(string t) => new Pill(t, redF, redB);
        public static Pill Gray(string t) => new Pill(t, grayF, grayB);
        public static Pill Blue(string t) => new Pill(t, blueF, blueB);
        public static Color Danger => redF;

        /// <summary>Scales a size in 96-DPI pixels to the screen's DPI.</summary>
        public static int S(int px) => (int)Math.Round(px * Scale);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Glyphs from Segoe Fluent Icons (Windows 11) / Segoe MDL2 Assets (Windows 10).
        public const string IconShield = "", IconApps = "", IconSettings = "",
                            IconHistory = "", IconAdmin = "", IconSearch = "";

        static Font IconFont(float size)
        {
            foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                var f = new Font(name, size);
                if (f.Name == name) return f;
                f.Dispose();
            }
            return new Font("Segoe UI Symbol", size);
        }

        static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        static bool WindowsUsesDarkApps()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Makes the window's title bar dark to match dark mode (Windows 10 2004+ / 11).</summary>
        public static void ApplyTitleBar(IntPtr hwnd)
        {
            if (!Dark) return;
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
        }
    }
}
