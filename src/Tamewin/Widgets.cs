using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Tamewin.Theme;

namespace Tamewin
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

    /// <summary>
    /// Scrolling list that stacks its visible children top to bottom at full width.
    /// Scrolls itself (no WinForms AutoScroll) and draws its own scrollbar in the right-hand gutter,
    /// so the bar is always visible and works with the wheel, dragging, clicking and the keyboard.
    /// </summary>
    public sealed class ListPanel : Panel
    {
        [DllImport("user32.dll")]
        static extern int ScrollWindowEx(IntPtr hWnd, int dx, int dy, IntPtr scroll, IntPtr clip, IntPtr rgnUpdate, IntPtr rcUpdate, uint flags);
        const uint SW_SCROLLCHILDREN = 0x1, SW_INVALIDATE = 0x2;

        int offset, target, contentHeight;
        bool barHover, dragging;
        int dragStartY, dragStartOffset;
        readonly Timer glide = new Timer { Interval = 10 };

        public ListPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = false;
            BackColor = Window;
            Dock = DockStyle.Fill;
            Padding = new Padding(S(30), 0, S(30), S(20));
            Margin = Padding.Empty;
            glide.Tick += (s, e) => GlideStep();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) glide.Dispose();
            base.Dispose(disposing);
        }

        int MaxOffset => Math.Max(0, contentHeight - ClientSize.Height);
        bool CanScroll => MaxOffset > 0;
        int Clamp(int y) => Math.Max(0, Math.Min(MaxOffset, y));

        // Wheel notches add up from where the scroll is heading, so fast wheeling keeps going.
        public void ScrollBy(int wheelDelta) => ScrollTo(target - wheelDelta * S(100) / 120);

        /// <summary>Scrolls to y. Animated by default; dragging the bar jumps straight there.</summary>
        public void ScrollTo(int y, bool animate = true)
        {
            target = Clamp(y);
            if (!animate || !IsHandleCreated) { glide.Stop(); MoveTo(target); }
            else if (target != offset) glide.Start();
        }

        // Eases towards the target: each tick covers a third of the remaining distance.
        void GlideStep()
        {
            int left = target - offset;
            if (left == 0) { glide.Stop(); return; }
            int step = left / 3;
            if (step == 0) step = Math.Sign(left);
            MoveTo(offset + step);
        }

        // Shifts what's already on screen (children included) instead of moving and repainting every row.
        void MoveTo(int y)
        {
            y = Clamp(y);
            int dy = offset - y;
            if (dy == 0) return;
            offset = y;
            if (IsHandleCreated)
            {
                ScrollWindowEx(Handle, 0, dy, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, SW_SCROLLCHILDREN | SW_INVALIDATE);
                Invalidate(new Rectangle(Track.Left - S(4), 0, ClientSize.Width - Track.Left + S(4), ClientSize.Height));
                Update();
            }
            else PerformLayout();
        }

        /// <summary>Scrolls just enough to show the control (used when Tab moves focus to a hidden row).</summary>
        public void EnsureVisible(Control c)
        {
            if (c.Top < 0) ScrollTo(offset + c.Top - S(8));
            else if (c.Bottom > ClientSize.Height) ScrollTo(offset + c.Bottom - ClientSize.Height + S(8));
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.GotFocus += (s, a) => EnsureVisible((Control)s);
            e.Control.KeyDown += (s, a) => ScrollKey(a);
        }

        void ScrollKey(KeyEventArgs e)
        {
            int page = Math.Max(S(60), ClientSize.Height - S(60));
            switch (e.KeyCode)
            {
                case Keys.PageDown: ScrollTo(target + page); break;
                case Keys.PageUp: ScrollTo(target - page); break;
                case Keys.Home when e.Control: ScrollTo(0); break;
                case Keys.End when e.Control: ScrollTo(MaxOffset); break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnMouseWheel(MouseEventArgs e) => ScrollBy(e.Delta);

        protected override void OnLayout(LayoutEventArgs e)
        {
            int w = ClientSize.Width - Padding.Horizontal;
            if (w <= 0) return;
            int y = Padding.Top;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (c is OptionRow row) row.FitWidth(w); else c.Width = w;
                y += c.Height + c.Margin.Bottom;
            }
            contentHeight = y + Padding.Bottom;
            offset = Clamp(offset);
            target = Clamp(target);

            y = Padding.Top - offset;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                c.Location = new Point(Padding.Left, y);
                y += c.Height + c.Margin.Bottom;
            }
            Invalidate();
        }

        // ---- scrollbar, drawn in the right padding ----

        Rectangle Track => new Rectangle(ClientSize.Width - S(16), S(4), S(10), ClientSize.Height - S(8));

        Rectangle Thumb
        {
            get
            {
                var t = Track;
                int h = Math.Max(S(40), (int)((long)t.Height * ClientSize.Height / Math.Max(1, contentHeight)));
                h = Math.Min(h, t.Height);
                int y = t.Top + (MaxOffset == 0 ? 0 : (int)((long)(t.Height - h) * offset / MaxOffset));
                return new Rectangle(t.Left, y, t.Width, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!CanScroll) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var thumb = Thumb;
            bool wide = barHover || dragging;
            if (wide)
                using (var b = new SolidBrush(Sidebar))
                using (var path = Round(Track, Track.Width / 2))
                    g.FillPath(b, path);
            if (!wide) thumb = Rectangle.Inflate(thumb, -S(2), 0);
            using (var b = new SolidBrush(dragging ? Theme.Text : TextDim))
            using (var path = Round(thumb, thumb.Width / 2))
                g.FillPath(b, path);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                var t = Track;
                int room = Math.Max(1, t.Height - Thumb.Height);
                ScrollTo(dragStartOffset + (int)((long)(e.Y - dragStartY) * MaxOffset / room), animate: false);
                return;
            }
            bool over = CanScroll && e.X >= Track.Left - S(6);
            if (over != barHover) { barHover = over; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (barHover && !dragging) { barHover = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !CanScroll || e.X < Track.Left - S(6)) return;
            var thumb = Thumb;
            if (e.Y < thumb.Top) ScrollTo(target - ClientSize.Height);            // click above: page up
            else if (e.Y > thumb.Bottom) ScrollTo(target + ClientSize.Height);    // click below: page down
            else { glide.Stop(); target = offset; dragging = true; dragStartY = e.Y; dragStartOffset = offset; Capture = true; }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            barHover = ClientRectangle.Contains(e.Location) && e.X >= Track.Left - S(6);
            Invalidate();
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
