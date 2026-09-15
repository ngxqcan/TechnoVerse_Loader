using System.Drawing;

namespace TechnoVerseLoader.UI
{
    public static class Theme
    {
        // Tone màu Tím Vũ Trụ (Cosmic Galaxy / Deep Purple Neon)
        public static readonly Color BgDark = Color.FromArgb(12, 10, 23);
        public static readonly Color Surface = Color.FromArgb(22, 18, 38);
        public static readonly Color CardBg = Color.FromArgb(31, 25, 54);
        public static readonly Color CardBgHover = Color.FromArgb(43, 35, 74);
        
        public static readonly Color Border = Color.FromArgb(67, 53, 110);
        public static readonly Color BorderActive = Color.FromArgb(168, 85, 247);

        public static readonly Color Primary = Color.FromArgb(124, 58, 237);       // Cosmic Purple
        public static readonly Color PrimaryHover = Color.FromArgb(109, 40, 217);  // Deep Purple
        public static readonly Color PrimaryActive = Color.FromArgb(91, 33, 182);

        public static readonly Color CosmicViolet = Color.FromArgb(192, 132, 252); // Neon Violet
        public static readonly Color CosmicPink = Color.FromArgb(244, 114, 182);   // Starlight Pink
        public static readonly Color Cyan = Color.FromArgb(56, 189, 248);          // Cosmic Cyan
        public static readonly Color Green = Color.FromArgb(52, 211, 153);         // Emerald Mint
        public static readonly Color Yellow = Color.FromArgb(251, 191, 36);        // Solar Gold
        public static readonly Color Red = Color.FromArgb(244, 63, 94);            // Supernova Red

        public static readonly Color TextWhite = Color.FromArgb(255, 255, 255);
        public static readonly Color TextGray = Color.FromArgb(221, 214, 254);     // Soft Lilac
        public static readonly Color TextMuted = Color.FromArgb(139, 127, 168);    // Muted Purple

        public static readonly Font TitleFont = new Font("Segoe UI", 13F, FontStyle.Bold);
        public static readonly Font HeaderFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        public static readonly Font SubFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        public static readonly Font CodeFont = new Font("Consolas", 9.5F, FontStyle.Bold);
    }
}
