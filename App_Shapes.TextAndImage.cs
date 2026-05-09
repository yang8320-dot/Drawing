using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public class TextNodeShape : ShapeBase
        {
            [Browsable(false)]
            public bool IsTransparent { get; set; } = false;
            
            public TextNodeShape() { } 
            public TextNodeShape(PointF start, Color color, bool transparent) : base(start, color)
            {
                IsTransparent = transparent;
                Text = "雙擊編輯";
            }
            public override void Draw(Graphics g)
            {
                if (!IsTransparent)
                {
                    if (EnableShadow)
                        g.FillRectangle(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                    
                    if (FillColor != Color.Transparent)
                        g.FillRectangle(GetCachedFillBrush(Bounds), Bounds);
                    
                    g.DrawRectangle(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                }
                DrawText(g);
            }
        }

        public class ImageShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;
            [Browsable(false)] public override TextAlign TextAlignment { get; set; } = TextAlign.MiddleCenter;

            private static Dictionary<string, Bitmap> _globalImageCache = new Dictionary<string, Bitmap>();

            [Browsable(false)]
            public string Base64Image { get; set; }
            
            public ImageShape() { }
            public ImageShape(PointF start, Bitmap img) : base(start, Color.Black)
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    Base64Image = Convert.ToBase64String(ms.ToArray());
                }
                
                if (!_globalImageCache.ContainsKey(Base64Image))
                {
                    _globalImageCache[Base64Image] = new Bitmap(img);
                }
                
                Bounds = new RectangleF(Bounds.X, Bounds.Y, img.Width, img.Height); 
            }

            public override void Draw(Graphics g)
            {
                if (!string.IsNullOrEmpty(Base64Image))
                {
                    if (!_globalImageCache.ContainsKey(Base64Image))
                    {
                        using (var ms = new System.IO.MemoryStream(Convert.FromBase64String(Base64Image)))
                        {
                            _globalImageCache[Base64Image] = new Bitmap(ms);
                        }
                    }

                    Bitmap imgToDraw = _globalImageCache[Base64Image];

                    if (EnableShadow)
                        g.FillRectangle(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                    
                    g.DrawImage(imgToDraw, Bounds);
                }
                DrawText(g);
            }
        }
    }
}
