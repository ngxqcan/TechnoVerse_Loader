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
}
