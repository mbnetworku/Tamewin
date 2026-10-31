using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Fastyourpc.Theme;

namespace Fastyourpc
{
    /// <summary>Base for the custom-drawn controls: double-buffered, tracks hover and press.</summary>
    public class PaintedControl : Control
    {
        protected bool Hover, Pressed;

        public PaintedControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected Color ParentBack => Parent?.BackColor ?? Window;

        protected Graphics Begin(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return g;
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = Pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        /// <summary>Passes the mouse wheel to the scrolling list this control sits in.</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Parent is ListPanel list) list.ScrollBy(e.Delta); else base.OnMouseWheel(e);
        }

        protected static void DrawFocus(Graphics g, Rectangle r, int radius)
        {
            using (var pen = new Pen(Theme.Text, S(2)))
            using (var path = Round(Rectangle.Inflate(r, -1, -1), radius))
                g.DrawPath(pen, path);
        }
    }

    public enum ButtonKind { Primary, Secondary, Subtle }

    public sealed class FlatButton : PaintedControl
    {
        public ButtonKind Kind;
        public string Glyph;

        public FlatButton(string text, ButtonKind kind, EventHandler onClick, string glyph = null)
        {
            Kind = kind; Glyph = glyph;
            Font = kind == ButtonKind.Primary ? BodyBold : Body;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            Margin = new Padding(S(4), S(2), S(4), S(2));
            Text = text;
            if (onClick != null) Click += onClick;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            var w = TextRenderer.MeasureText(Text, Font).Width + S(Glyph != null ? 52 : 32);
            Size = new Size(w, S(34));
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color back, fore, border = Color.Empty;
            if (!Enabled)
            {
                back = Kind == ButtonKind.Subtle ? ParentBack : Border; fore = TextDisabled;
            }
            else if (Kind == ButtonKind.Primary)
            {
                back = Hover ? AccentHover : Accent; fore = OnAccent;
            }
            else if (Kind == ButtonKind.Secondary)
            {
                back = Pressed ? Window : Hover ? CardHover : Card; fore = Theme.Text; border = Theme.Border;
            }
            else
            {
                back = Hover ? (Dark ? Color.FromArgb(24, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0)) : ParentBack; fore = Accent;
            }
            using (var path = Round(r, S(5)))
            {
                using (var b = new SolidBrush(back)) g.FillPath(b, path);
                if (border != Color.Empty) using (var p = new Pen(border)) g.DrawPath(p, path);
            }
            var textRect = r;
            if (Glyph != null)
            {
                var gr = new Rectangle(S(12), 0, S(20), Height);
                TextRenderer.DrawText(g, Glyph, Icons, gr, fore, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                textRect = new Rectangle(S(34), 0, Width - S(46), Height);
            }
            TextRenderer.DrawText(g, Text, Font, textRect, fore,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | (Glyph != null ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
            if (Focused && ShowFocusCues) DrawFocus(g, r, S(5));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    /// <summary>A card with a checkbox, title, description and coloured pills. Click anywhere to tick it.</summary>
    public sealed class OptionRow : PaintedControl
    {
        public string Title, Description;
        public List<Pill> Pills = new List<Pill>();
        public object Item;
        public bool Locked;                 // can't be ticked, but can still be clicked to highlight
        public Func<bool> ConfirmCheck;     // asked before ticking; return false to cancel
        public event EventHandler CheckedChanged, Activated;

        bool isChecked, highlighted;
        int titleH, descH;

        public bool Checked
        {
            get => isChecked;
            set { if (isChecked == value) return; isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public bool Highlighted { get => highlighted; set { highlighted = value; Invalidate(); } }

        public OptionRow()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
            Margin = new Padding(0, 0, 0, S(4));
        }

        int Pad => S(16);
        int Box => S(20);
        int TextLeft => Pad + Box + S(16);

        static Size PillSize(Pill p)
        {
            var s = TextRenderer.MeasureText(p.Text, SmallBold, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return new Size(s.Width + S(18), s.Height + S(8));
        }

        int PillsWidth => Pills.Count == 0 ? 0 : Pills.Sum(p => PillSize(p).Width) + S(6) * (Pills.Count - 1) + S(12);
        int TextWidth => Math.Max(S(120), Width - TextLeft - PillsWidth - Pad);

        /// <summary>Sets the width and works out the height the wrapped text needs.</summary>
        public void FitWidth(int width)
        {
            Width = width;
            const TextFormatFlags f = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            titleH = TextRenderer.MeasureText(Title ?? "", BodyBold, new Size(TextWidth, 0), f).Height;
            descH = string.IsNullOrEmpty(Description) ? 0 : TextRenderer.MeasureText(Description, Theme.Desc, new Size(TextWidth, 0), f).Height;
            Height = Math.Max(S(60), S(13) + titleH + (descH > 0 ? S(3) + descH : 0) + S(13));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Round(r, S(6)))
            {
                using (var b = new SolidBrush(Hover ? CardHover : Card)) g.FillPath(b, path);
                using (var p = new Pen(highlighted ? Accent : Theme.Border, highlighted ? S(2) : 1)) g.DrawPath(p, path);
            }

            // checkbox
            var box = new Rectangle(Pad, (Height - Box) / 2, Box, Box);
            using (var path = Round(box, S(4)))
            {
                if (Locked)
                {
                    using (var b = new SolidBrush(Window)) g.FillPath(b, path);
                    using (var p = new Pen(TextDisabled)) g.DrawPath(p, path);
                }
                else if (isChecked)
                {
                    using (var b = new SolidBrush(Accent)) g.FillPath(b, path);
                    using (var p = new Pen(OnAccent, S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(p, new[]
                        {
                            new PointF(box.X + box.Width * 0.26f, box.Y + box.Height * 0.52f),
                            new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.70f),
                            new PointF(box.X + box.Width * 0.76f, box.Y + box.Height * 0.32f),
                        });
                }
                else
                {
                    using (var b = new SolidBrush(Hover ? CardHover : Card)) g.FillPath(b, path);
                    using (var p = new Pen(TextDim, 1.5f)) g.DrawPath(p, path);
                }
            }

            // text
            const TextFormatFlags f = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.Left;
            int top = (Height - (titleH + (descH > 0 ? S(3) + descH : 0))) / 2;
            TextRenderer.DrawText(g, Title, BodyBold, new Rectangle(TextLeft, top, TextWidth, titleH), Locked ? TextDim : Theme.Text, f);
            if (descH > 0)
                TextRenderer.DrawText(g, Description, Theme.Desc, new Rectangle(TextLeft, top + titleH + S(3), TextWidth, descH),
                    Locked ? TextDisabled : TextDim, f);

            // pills, right-aligned
            int x = Width - Pad;
            for (int i = Pills.Count - 1; i >= 0; i--)
            {
                var sz = PillSize(Pills[i]);
                x -= sz.Width;
                var pr = new Rectangle(x, (Height - sz.Height) / 2, sz.Width, sz.Height);
                using (var path = Round(pr, sz.Height / 2))
                using (var b = new SolidBrush(Pills[i].Back))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, Pills[i].Text, SmallBold, pr, Pills[i].Fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                x -= S(6);
            }

            if (Focused && ShowFocusCues) DrawFocus(g, r, S(6));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); Activated?.Invoke(this, EventArgs.Empty); Toggle(); }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Activated?.Invoke(this, EventArgs.Empty); Toggle(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Toggle()
        {
            if (Locked) return;
            if (!isChecked && ConfirmCheck != null && !ConfirmCheck()) return;
            Checked = !isChecked;
        }
    }

    /// <summary>Group title inside a list, e.g. "Search", with an optional dim count after it.</summary>
    public sealed class SectionHeader : PaintedControl
    {
        public string Count;

        public SectionHeader(string text)
        {
            Text = text;
            Height = S(44);
            Margin = new Padding(0, 0, 0, S(2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e);
            var size = TextRenderer.MeasureText(Text, Section, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            int y = Height - size.Height - S(8);
            TextRenderer.DrawText(g, Text, Section, new Point(S(2), y), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (!string.IsNullOrEmpty(Count))
                TextRenderer.DrawText(g, Count, Theme.Desc, new Point(S(2) + size.Width + S(10), y + S(3)), TextDim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Big page title with a one-line subtitle under it.</summary>
    public sealed class HeaderBar : PaintedControl
    {
        public string Title, Subtitle;

        public HeaderBar()
        {
            Dock = DockStyle.Fill;
            Height = S(88);
            Margin = Padding.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e);
            TextRenderer.DrawText(g, Title, PageTitle, new Point(S(30), S(20)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Subtitle, Body, new Rectangle(S(32), S(60), Width - S(64), S(24)), TextDim,
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>A sidebar page link with an icon and an accent marker when selected.</summary>
    public sealed class NavItem : PaintedControl
    {
        public string Glyph;
        bool selected;
        public bool Selected { get => selected; set { selected = value; Invalidate(); } }

        public NavItem(string glyph, string text)
        {
            Glyph = glyph; Text = text;
            Size = new Size(S(224), S(40));
            Margin = new Padding(0, 0, 0, S(3));
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Begin(e);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (selected || Hover)
                using (var path = Round(r, S(5)))
                using (var b = new SolidBrush(selected ? Theme.Selected : (Dark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0))))
                    g.FillPath(b, path);
            if (selected)
                using (var path = Round(new Rectangle(0, (Height - S(18)) / 2, S(3), S(18)), S(2)))
                using (var b = new SolidBrush(Accent))
                    g.FillPath(b, path);
            TextRenderer.DrawText(g, Glyph, Icons, new Rectangle(S(14), 0, S(24), Height), Theme.Text,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, selected ? BodyBold : Body, new Rectangle(S(48), 0, Width - S(52), Height), Theme.Text,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) DrawFocus(g, r, S(5));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    /// <summary>Scrolling list that stacks its visible children top to bottom at full width.</summary>
    public sealed class ListPanel : Panel
    {
        public ListPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
            BackColor = Window;
            Dock = DockStyle.Fill;
            Padding = new Padding(S(30), 0, S(30), S(20));
            Margin = Padding.Empty;
        }

        public void ScrollBy(int wheelDelta)
        {
            int y = -AutoScrollPosition.Y - wheelDelta * S(60) / 120;
            AutoScrollPosition = new Point(0, Math.Max(0, y));
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int w = ClientSize.Width - Padding.Horizontal;
            if (w <= 0) return;
            int y = AutoScrollPosition.Y + Padding.Top;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (c is OptionRow row) row.FitWidth(w); else c.Width = w;
                c.Location = new Point(Padding.Left, y);
                y += c.Height + c.Margin.Bottom;
            }
            var needed = new Size(0, y - AutoScrollPosition.Y + Padding.Bottom);
            if (AutoScrollMinSize != needed) AutoScrollMinSize = needed;
        }
    }

    /// <summary>Bottom bar of a page: a summary on the left, action buttons on the right.</summary>
    public sealed class Footer : Panel
    {
        public readonly Label Summary = new Label
        {
            Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = Body, ForeColor = TextDim, BackColor = Color.Transparent,
        };
        public readonly FlowLayoutPanel Buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, S(3), 0, 0),
        };

        public Footer()
        {
            Dock = DockStyle.Fill;
            Height = S(68);
            Margin = Padding.Empty;
            Padding = new Padding(S(30), S(12), S(26), S(12));
            BackColor = Window;
            DoubleBuffered = true;
            Controls.Add(Summary);
            Controls.Add(Buttons);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }

    /// <summary>The layout every page uses: header, toolbar, content (usually a list), footer.</summary>
    public class PageLayout : TableLayoutPanel
    {
        public readonly HeaderBar Header = new HeaderBar();
        public readonly FlowLayoutPanel Toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BackColor = Window,
            Padding = new Padding(S(26), 0, S(26), S(8)), Margin = Padding.Empty,
        };
        public readonly Footer Footer = new Footer();
        public readonly Control Content;

        public PageLayout(string title, string subtitle, Control content = null)
        {
            Content = content ?? new ListPanel();
            Header.Title = title;
            Header.Subtitle = subtitle;
            Dock = DockStyle.Fill;
            BackColor = Window;
            Margin = Padding.Empty;
            ColumnCount = 1;
            RowCount = 4;
            RowStyles.Add(new RowStyle(SizeType.Absolute, Header.Height));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            RowStyles.Add(new RowStyle(SizeType.Absolute, Footer.Height));
            Controls.Add(Header, 0, 0);
            Controls.Add(Toolbar, 0, 1);
            Controls.Add(Content, 0, 2);
            Controls.Add(Footer, 0, 3);
        }

        public ListPanel List => Content as ListPanel;
    }

    public static class TextBoxExtensions
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Grey hint text shown while the box is empty.</summary>
        public static void SetPlaceholder(this TextBox box, string text)
        {
            void Apply() => SendMessage(box.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, text);
            if (box.IsHandleCreated) Apply(); else box.HandleCreated += (s, e) => Apply();
        }
    }
}
