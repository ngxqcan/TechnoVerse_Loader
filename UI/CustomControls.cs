using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TechnoVerseLoader.UI
{
    public static class GraphicsUtils
    {
        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class CyberButton : Button
    {
        public int BorderRadius { get; set; } = 8;
        public Color NormalColor { get; set; } = Theme.Primary;
        public Color HoverColor { get; set; } = Theme.PrimaryHover;
        public Color PressedColor { get; set; } = Theme.PrimaryActive;
        public Color BorderLineColor { get; set; } = Color.Transparent;

        private bool _isHovered;
        private bool _isPressed;

        public CyberButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Theme.TextWhite;
            Font = Theme.HeaderFont;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            Size = new Size(160, 42);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _isPressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Clear corners with parent background to eliminate any clipping artifacts
            Color parentBg = Parent?.BackColor ?? Color.FromArgb(14, 11, 26);
            if (parentBg == Color.Transparent && Parent?.Parent != null)
            {
                parentBg = Parent.Parent.BackColor;
            }
            if (parentBg == Color.Transparent)
            {
                parentBg = Color.FromArgb(14, 11, 26);
            }

            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GraphicsUtils.CreateRoundedRectangle(rect, BorderRadius);

            Color currentBg = !Enabled ? Color.FromArgb(40, 35, 55) :
                (_isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor));

            using var brush = new SolidBrush(currentBg);
            g.FillPath(brush, path);

            if (BorderLineColor != Color.Transparent)
            {
                using var pen = new Pen(BorderLineColor, 1.0f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                g,
                Text,
                Font,
                rect,
                !Enabled ? Theme.TextMuted : (_isHovered ? Theme.TextWhite : ForeColor),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
    }

    public class CardPanel : Panel
    {
        public int BorderRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Theme.Border;
        public Color BackgroundColor { get; set; } = Theme.CardBg;

        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Padding = new Padding(12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = Parent?.BackColor ?? Color.FromArgb(14, 11, 26);
            if (parentBg == Color.Transparent && Parent?.Parent != null)
            {
                parentBg = Parent.Parent.BackColor;
            }
            if (parentBg == Color.Transparent)
            {
                parentBg = Color.FromArgb(14, 11, 26);
            }

            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GraphicsUtils.CreateRoundedRectangle(rect, BorderRadius);

            using var bgBrush = new SolidBrush(BackgroundColor);
            g.FillPath(bgBrush, path);

            if (BorderColor != Color.Transparent)
            {
                using var borderPen = new Pen(BorderColor, 1f);
                g.DrawPath(borderPen, path);
            }
        }
    }

    public class CyberProgressBar : Control
    {
        private int _value = 0;
        public int Maximum { get; set; } = 100;
        public Color ProgressColor1 { get; set; } = Theme.Primary;
        public Color ProgressColor2 { get; set; } = Theme.Cyan;
        public Color BackgroundBarColor { get; set; } = Theme.Surface;
        public int BorderRadius { get; set; } = 6;
        public bool ShowPercentage { get; set; } = true;

        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Clamp(value, 0, Maximum);
                Invalidate();
            }
        }

        public CyberProgressBar()
        {
            DoubleBuffered = true;
            Height = 18;
            Width = 200;
            Font = Theme.SmallFont;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var pathBg = GraphicsUtils.CreateRoundedRectangle(rect, BorderRadius);

            using var bgBrush = new SolidBrush(BackgroundBarColor);
            g.FillPath(bgBrush, pathBg);

            int progressWidth = (int)((float)Value / Maximum * rect.Width);
            if (progressWidth > 4)
            {
                var progressRect = new Rectangle(0, 0, progressWidth, rect.Height);
                using var pathProgress = GraphicsUtils.CreateRoundedRectangle(progressRect, BorderRadius);
                using var gradientBrush = new LinearGradientBrush(
                    rect, ProgressColor1, ProgressColor2, LinearGradientMode.Horizontal
                );
                g.FillPath(gradientBrush, pathProgress);
            }

            using var borderPen = new Pen(Theme.Border, 1f);
            g.DrawPath(borderPen, pathBg);

            if (ShowPercentage)
            {
                string text = $"{Value}%";
                TextRenderer.DrawText(
                    g,
                    text,
                    Font,
                    rect,
                    Theme.TextWhite,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                );
            }
        }
    }

    public class CyberTextBox : Panel
    {
        private readonly TextBox _textBox;
        public int BorderRadius { get; set; } = 8;
        public Color BorderLineColor { get; set; } = Theme.Border;
        public Color FocusBorderColor { get; set; } = Theme.Primary;
        public string PlaceholderText { get; set; } = "";

        private bool _isFocused = false;

        public TextBox InnerTextBox => _textBox;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? "";
        }

        public CyberTextBox()
        {
            DoubleBuffered = true;
            Height = 42;
            Padding = new Padding(12, 10, 12, 10);
            BackColor = Color.Transparent;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextWhite,
                Font = Theme.CodeFont,
                Dock = DockStyle.Fill
            };

            _textBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _textBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };

            Controls.Add(_textBox);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GraphicsUtils.CreateRoundedRectangle(rect, BorderRadius);

            using var bgBrush = new SolidBrush(Theme.Surface);
            g.FillPath(bgBrush, path);

            using var pen = new Pen(_isFocused ? FocusBorderColor : BorderLineColor, 1.2f);
            g.DrawPath(pen, path);
        }
    }

    public class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(24, 19, 42);
        public override Color ImageMarginGradientBegin => Color.FromArgb(24, 19, 42);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(24, 19, 42);
        public override Color ImageMarginGradientEnd => Color.FromArgb(24, 19, 42);
        public override Color MenuBorder => Theme.BorderActive;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.Primary;
        public override Color MenuItemSelectedGradientBegin => Theme.Primary;
        public override Color MenuItemSelectedGradientEnd => Theme.Primary;
        public override Color MenuItemPressedGradientBegin => Theme.PrimaryActive;
        public override Color MenuItemPressedGradientEnd => Theme.PrimaryActive;
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using var brush = new SolidBrush(Theme.Primary);
                e.Graphics.FillRectangle(brush, new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2));
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(24, 19, 42));
                e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.TextWhite;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.BorderActive, 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    public class CyberDropdown : Control
    {
        private readonly List<string> _items = new();
        private int _selectedIndex = 0;
        private bool _isHovered = false;
        private readonly ContextMenuStrip _menu;

        public event EventHandler? SelectedIndexChanged;

        public Color BorderColor { get; set; } = Theme.Border;
        public Color BorderHoverColor { get; set; } = Theme.BorderActive;
        public Color FillColor { get; set; } = Color.FromArgb(28, 22, 48);
        public Color FillHoverColor { get; set; } = Color.FromArgb(38, 30, 64);
        public Color ArrowColor { get; set; } = Theme.CosmicViolet;
        public int BorderRadius { get; set; } = 6;

        public IList<string> Items => _items;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value >= 0 && value < _items.Count && _selectedIndex != value)
                {
                    _selectedIndex = value;
                    Invalidate();
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string SelectedText => _items.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _items.Count
            ? _items[_selectedIndex]
            : "";

        public CyberDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(100, 24);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            ForeColor = Theme.TextWhite;

            _menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                BackColor = Color.FromArgb(24, 19, 42),
                ForeColor = Theme.TextWhite,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Renderer = new DarkMenuRenderer(),
                DropShadowEnabled = false
            };
        }

        public void RebuildMenu()
        {
            _menu.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(_items[i])
                {
                    ForeColor = Theme.TextWhite,
                    Height = 24,
                    Padding = new Padding(8, 3, 8, 3),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };
                item.Click += (s, e) =>
                {
                    SelectedIndex = idx;
                };
                _menu.Items.Add(item);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _items.Count > 0)
            {
                RebuildMenu();
                _menu.MinimumSize = new Size(Width, 0);
                _menu.Show(this, new Point(0, Height + 2));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Xóa sạch nền bằng màu nền thực sự của CardPanel cha (loại bỏ hoàn toàn vệt trắng 4 góc bo)
            Color parentBg = Color.FromArgb(22, 17, 40);
            if (Parent is CardPanel cp)
            {
                parentBg = cp.BackgroundColor;
            }
            else if (Parent != null && Parent.BackColor != Color.Transparent)
            {
                parentBg = Parent.BackColor;
            }

            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GraphicsUtils.CreateRoundedRectangle(rect, BorderRadius);

            // Fill background
            using (var brush = new SolidBrush(_isHovered ? FillHoverColor : FillColor))
            {
                g.FillPath(brush, path);
            }

            // Draw border
            using (var pen = new Pen(_isHovered ? BorderHoverColor : BorderColor, 1.2f))
            {
                g.DrawPath(pen, path);
            }

            // Draw text
            string text = SelectedText;
            var textRect = new Rectangle(8, 0, Width - 24, Height);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using (var textBrush = new SolidBrush(ForeColor))
            {
                g.DrawString(text, Font, textBrush, textRect, sf);
            }

            // Draw clean dropdown triangle arrow
            int arrowX = Width - 12;
            int arrowY = Height / 2 - 1;
            Point[] arrowPoints = new Point[]
            {
                new Point(arrowX - 4, arrowY - 2),
                new Point(arrowX + 4, arrowY - 2),
                new Point(arrowX, arrowY + 3)
            };
            using (var arrowBrush = new SolidBrush(_isHovered ? Theme.TextWhite : ArrowColor))
            {
                g.FillPolygon(arrowBrush, arrowPoints);
            }
        }
    }
}
