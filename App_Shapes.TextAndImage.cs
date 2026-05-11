using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Newtonsoft.Json;

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
                    if (ShouldDrawShadow)
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
            
            // 【修正6】: 字型預設改為標楷體
            [Browsable(false)] public override string FontName { get; set; } = "標楷體";
            
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;
            [Browsable(false)] public override TextAlign TextAlignment { get; set; } = TextAlign.MiddleCenter;

            [Browsable(false)]
            public string Base64Image { get; set; }
            
            [JsonIgnore]
            [Browsable(false)]
            private Bitmap _localImage;
            
            public ImageShape() { }
            public ImageShape(PointF start, Bitmap img) : base(start, Color.Black)
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    Base64Image = Convert.ToBase64String(ms.ToArray());
                }
                
                _localImage = new Bitmap(img);
                Bounds = new RectangleF(Bounds.X, Bounds.Y, img.Width, img.Height); 
            }

            public override void Draw(Graphics g)
            {
                if (!string.IsNullOrEmpty(Base64Image))
                {
                    if (_localImage == null)
                    {
                        using (var ms = new System.IO.MemoryStream(Convert.FromBase64String(Base64Image)))
                        {
                            _localImage = new Bitmap(ms);
                        }
                    }

                    if (ShouldDrawShadow)
                        g.FillRectangle(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                    
                    g.DrawImage(_localImage, Bounds);
                }
                DrawText(g);
            }

            public override void Dispose()
            {
                base.Dispose();
                if (_localImage != null)
                {
                    _localImage.Dispose();
                    _localImage = null;
                }
            }
        }
    }
}
