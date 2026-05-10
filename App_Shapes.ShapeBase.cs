using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using Newtonsoft.Json;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public abstract class ShapeBase : IDisposable
        {
            protected static readonly Brush SharedShadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

            protected Pen _cachedPen;
            protected Brush _cachedFillBrush;
            protected Font _cachedFont;
            protected Brush _cachedTextBrush;
            private RectangleF _lastBrushBounds;

            // [優化 2]：新增全局快速渲染開關
            [Browsable(false)]
            [JsonIgnore]
            public static bool IsFastRendering { get; set; } = false;

            // [優化 2]：智慧判斷當前是否應該畫陰影
            [Browsable(false)]
            [JsonIgnore]
            protected bool ShouldDrawShadow => EnableShadow && !IsFastRendering;

            private RectangleF _bounds;
            [Category("3. 座標與尺寸")]
            [DisplayName("物件邊界 (Bounds)")]
            [Description("修改物件的 X, Y 座標與寬高。")]
            public virtual RectangleF Bounds 
            { 
                get => _bounds; 
                set { _bounds = value; InvalidateBrush(); } 
            }

            private Color _shapeColor;
            [Category("1. 外觀屬性")]
            [DisplayName("外框/線條顏色")]
            public virtual Color ShapeColor 
            { 
                get => _shapeColor; 
                set { if (_shapeColor != value) { _shapeColor = value; InvalidatePen(); } } 
            }

            private Color _fillColor = Color.Transparent;
            [Category("1. 外觀屬性")]
            [DisplayName("填充顏色")]
            [Description("圖形內部的顏色，預設為透明 (Transparent)。")]
            public virtual Color FillColor 
            { 
                get => _fillColor; 
                set { if (_fillColor != value) { _fillColor = value; InvalidateBrush(); } } 
            }
            
            private BrushType _fillBrushType = BrushType.Solid;
            [Category("1. 外觀屬性")]
            [DisplayName("填充筆刷類型")]
            public virtual BrushType FillBrushType 
            { 
                get => _fillBrushType; 
                set { if (_fillBrushType != value) { _fillBrushType = value; InvalidateBrush(); } } 
            }

            private Color _gradientColor2 = Color.White;
            [Category("1. 外觀屬性")]
            [DisplayName("漸層次色")]
            public virtual Color GradientColor2 
            { 
                get => _gradientColor2; 
                set { if (_gradientColor2 != value) { _gradientColor2 = value; InvalidateBrush(); } } 
            }

            [Category("1. 外觀屬性")]
            [DisplayName("啟用陰影")]
            public virtual bool EnableShadow { get; set; } = false;

            private float _strokeWidth = 2f;
            [Category("1. 外觀屬性")]
            [DisplayName("線條粗細")]
            public virtual float StrokeWidth 
            { 
                get => _strokeWidth; 
                set { if (_strokeWidth != value) { _strokeWidth = value; InvalidatePen(); } } 
            }

            private DashStyle _strokeDashStyle = DashStyle.Solid;
            [Category("1. 外觀屬性")]
            [DisplayName("線條樣式")]
            public virtual DashStyle StrokeDashStyle 
            { 
                get => _strokeDashStyle; 
                set { if (_strokeDashStyle != value) { _strokeDashStyle = value; InvalidatePen(); } } 
            }
            
            [Category("3. 座標與尺寸")]
            [DisplayName("旋轉角度")]
            public virtual float RotationAngle { get; set; } = 0f;
            
            [Browsable(false)]
            [JsonIgnore] 
            public bool IsSelected { get; set; }

            [Category("4. 系統屬性")]
            [DisplayName("鎖定圖形")]
            [Description("鎖定後將無法被拖曳或修改大小。")]
            public bool IsLocked { get; set; } = false;
            
            [Browsable(false)]
            public Guid Id { get; set; } = Guid.NewGuid();

            [Category("2. 文字屬性")]
            [DisplayName("文字內容")]
            public virtual string Text { get; set; } = "";

            private string _fontName = "Arial";
            [Category("2. 文字屬性")]
            [DisplayName("字型名稱")]
            public virtual string FontName 
            { 
                get => _fontName; 
                set { if (_fontName != value) { _fontName = value; InvalidateText(); } } 
            }

            private float _fontSize = 12f;
            [Category("2. 文字屬性")]
            [DisplayName("字體大小")]
            public virtual float FontSize 
            { 
                get => _fontSize; 
                set { if (_fontSize != value) { _fontSize = value; InvalidateText(); } } 
            }

            private Color _fontColor = Color.Black;
            [Category("2. 文字屬性")]
            [DisplayName("文字顏色")]
            public virtual Color FontColor 
            { 
                get => _fontColor; 
                set { if (_fontColor != value) { _fontColor = value; InvalidateText(); } } 
            }

            private bool _fontBold = false;
            [Category("2. 文字屬性")]
            [DisplayName("粗體")]
            public virtual bool FontBold 
            { 
                get => _fontBold; 
                set { if (_fontBold != value) { _fontBold = value; InvalidateText(); } } 
            }

            private bool _fontItalic = false;
            [Category("2. 文字屬性")]
            [DisplayName("斜體")]
            public virtual bool FontItalic 
            { 
                get => _fontItalic; 
                set { if (_fontItalic != value) { _fontItalic = value; InvalidateText(); } } 
            }

            private bool _fontUnderline = false;
            [Category("2. 文字屬性")]
            [DisplayName("底線")]
            public virtual bool FontUnderline 
            { 
                get => _fontUnderline; 
                set { if (_fontUnderline != value) { _fontUnderline = value; InvalidateText(); } } 
            }

            [Category("2. 文字屬性")]
            [DisplayName("對齊方式")]
            public virtual TextAlign TextAlignment { get; set; } = TextAlign.MiddleCenter;

            public ShapeBase() { }

            public ShapeBase(PointF start, Color color)
            {
                Bounds = new RectangleF(start.X, start.Y, 0, 0);
                ShapeColor = color;
            }

            protected virtual void InvalidatePen()
            {
                if (_cachedPen != null) { _cachedPen.Dispose(); _cachedPen = null; }
            }

            protected virtual void InvalidateBrush()
            {
                if (_cachedFillBrush != null) { _cachedFillBrush.Dispose(); _cachedFillBrush = null; }
            }

            protected virtual void InvalidateText()
            {
                if (_cachedFont != null) { _cachedFont.Dispose(); _cachedFont = null; }
                if (_cachedTextBrush != null) { _cachedTextBrush.Dispose(); _cachedTextBrush = null; }
            }

            protected Pen GetCachedPen()
            {
                if (_cachedPen == null)
                    _cachedPen = new Pen(ShapeColor, StrokeWidth) { DashStyle = StrokeDashStyle };
                return _cachedPen;
            }

            protected Brush GetCachedFillBrush(RectangleF rect)
            {
                if (FillColor == Color.Transparent) return Brushes.Transparent;

                if (_cachedFillBrush != null && FillBrushType == BrushType.LinearGradient && _lastBrushBounds != rect)
                    InvalidateBrush();

                if (_cachedFillBrush == null)
                {
                    // [優化 2]：如果開啟快速渲染，強制使用純色替代漸層，減少運算
                    if (FillBrushType == BrushType.Solid || rect.Width <= 0 || rect.Height <= 0 || IsFastRendering)
                        _cachedFillBrush = new SolidBrush(FillColor);
                    else
                    {
                        _cachedFillBrush = new LinearGradientBrush(rect, FillColor, GradientColor2, LinearGradientMode.ForwardDiagonal);
                        _lastBrushBounds = rect;
                    }
                }
                return _cachedFillBrush;
            }

            protected Font GetCachedFont()
            {
                if (_cachedFont == null)
                {
                    FontStyle style = FontStyle.Regular;
                    if (FontBold) style |= FontStyle.Bold;
                    if (FontItalic) style |= FontStyle.Italic;
                    if (FontUnderline) style |= FontStyle.Underline;
                    _cachedFont = new Font(FontName, Math.Max(1, FontSize), style);
                }
                return _cachedFont;
            }

            protected Brush GetCachedTextBrush()
            {
                if (_cachedTextBrush == null) _cachedTextBrush = new SolidBrush(FontColor);
                return _cachedTextBrush;
            }

            public virtual void Dispose() 
            {
                InvalidatePen();
                InvalidateBrush();
                InvalidateText();
            }

            public virtual void ApplyFormatFrom(ShapeBase source)
            {
                if (source == null) return;
                this.ShapeColor = source.ShapeColor;
                this.FillColor = source.FillColor;
                this.FillBrushType = source.FillBrushType;
                this.GradientColor2 = source.GradientColor2;
                this.EnableShadow = source.EnableShadow;
                this.StrokeWidth = source.StrokeWidth;
                this.StrokeDashStyle = source.StrokeDashStyle;
                this.FontName = source.FontName;
                this.FontSize = source.FontSize;
                this.FontColor = source.FontColor;
                this.FontBold = source.FontBold;
                this.FontItalic = source.FontItalic;
                this.FontUnderline = source.FontUnderline;
                this.TextAlignment = source.TextAlignment;
            }

            public void DrawWithTransform(Graphics g)
            {
                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                Draw(g);

                g.Transform = oldMatrix;
            }

            public abstract void Draw(Graphics g);

            protected void DrawText(Graphics g)
            {
                if (string.IsNullOrEmpty(Text)) return;

                using (StringFormat sf = new StringFormat())
                {
                    switch (TextAlignment)
                    {
                        case TextAlign.TopLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Near; break;
                        case TextAlign.TopCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Near; break;
                        case TextAlign.TopRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Near; break;
                        case TextAlign.MiddleLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; break;
                        case TextAlign.MiddleCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; break;
                        case TextAlign.MiddleRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Center; break;
                        case TextAlign.BottomLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Far; break;
                        case TextAlign.BottomCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Far; break;
                        case TextAlign.BottomRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Far; break;
                    }
                    
                    sf.Trimming = StringTrimming.Word;
                    sf.FormatFlags = 0; 
                    
                    RectangleF textBounds = Bounds;
                    textBounds.Inflate(-5, -5); 
                    if (textBounds.Width <= 0 || textBounds.Height <= 0) textBounds = Bounds;

                    g.DrawString(Text, GetCachedFont(), GetCachedTextBrush(), textBounds, sf);
                }
            }

            public virtual void UpdateEndPoint(PointF pt)
            {
                if (IsLocked) return;
                Bounds = new RectangleF(Bounds.X, Bounds.Y, pt.X - Bounds.X, pt.Y - Bounds.Y);
                NormalizeBounds();
            }

            public virtual void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                Bounds = newBounds;
                NormalizeBounds();
            }

            public virtual void NormalizeBounds()
            {
                float x = Math.Min(Bounds.X, Bounds.Right);
                float y = Math.Min(Bounds.Y, Bounds.Bottom);
                float w = Math.Abs(Bounds.Width);
                float h = Math.Abs(Bounds.Height);
                Bounds = new RectangleF(x, y, w, h);
            }

            public virtual void Move(float dx, float dy)
            {
                if (IsLocked) return;
                Bounds = new RectangleF(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
            }

            public PointF GetCenter()
            {
                return new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
            }

            public virtual bool HitTest(PointF pt)
            {
                PointF rotatedPt = RotatePoint(pt, GetCenter(), -RotationAngle);
                RectangleF hitBounds = Bounds;
                hitBounds.Inflate(5, 5);
                return hitBounds.Contains(rotatedPt);
            }
            
            public virtual void DrawSelection(Graphics g)
            {
                if (!IsSelected) return;

                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                Color outlineColor = IsLocked ? Color.Gray : Color.DodgerBlue;
                using (Pen p = new Pen(outlineColor, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(p, Rectangle.Round(Bounds));
                }

                float s = 8;
                PointF[] corners = new PointF[]
                {
                    new PointF(Bounds.Left, Bounds.Top),
                    new PointF(center.X, Bounds.Top),
                    new PointF(Bounds.Right, Bounds.Top),
                    new PointF(Bounds.Right, center.Y),
                    new PointF(Bounds.Right, Bounds.Bottom),
                    new PointF(center.X, Bounds.Bottom),
                    new PointF(Bounds.Left, Bounds.Bottom),
                    new PointF(Bounds.Left, center.Y)
                };

                Brush fillBrush = IsLocked ? Brushes.LightGray : Brushes.White;
                Pen borderPen = IsLocked ? Pens.Gray : Pens.DodgerBlue;

                foreach (var pt in corners)
                {
                    g.FillRectangle(fillBrush, pt.X - s/2, pt.Y - s/2, s, s);
                    g.DrawRectangle(borderPen, pt.X - s/2, pt.Y - s/2, s, s);
                }

                if (!IsLocked)
                {
                    PointF rotatePt = new PointF(center.X, Bounds.Top - 25);
                    g.DrawLine(Pens.DodgerBlue, center.X, Bounds.Top, rotatePt.X, rotatePt.Y);
                    g.FillEllipse(Brushes.LightGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.DarkGreen, rotatePt.X - 5, rotatePt.Y - 5, 10, 10);
                }

                g.Transform = oldMatrix;
            }

            public virtual HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected || IsLocked) return HandlePosition.None;

                PointF center = GetCenter();
                PointF rotatedPt = RotatePoint(pt, center, -RotationAngle);

                float s = 10;
                if (new RectangleF(center.X - s, Bounds.Top - 25 - s, s * 2, s * 2).Contains(rotatedPt)) return HandlePosition.Rotate;

                if (new RectangleF(Bounds.Left - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.NW;
                if (new RectangleF(center.X - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.N;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Top - s/2, s, s).Contains(rotatedPt)) return HandlePosition.NE;
                if (new RectangleF(Bounds.Right - s/2, center.Y - s/2, s, s).Contains(rotatedPt)) return HandlePosition.E;
                if (new RectangleF(Bounds.Right - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.SE;
                if (new RectangleF(center.X - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.S;
                if (new RectangleF(Bounds.Left - s/2, Bounds.Bottom - s/2, s, s).Contains(rotatedPt)) return HandlePosition.SW;
                if (new RectangleF(Bounds.Left - s/2, center.Y - s/2, s, s).Contains(rotatedPt)) return HandlePosition.W;
                
                return HandlePosition.None;
            }

            public PointF GetAnchorPoint(AnchorPosition pos)
            {
                PointF pt = GetCenter();
                switch (pos)
                {
                    case AnchorPosition.Top: pt = new PointF(pt.X, Bounds.Top); break;
                    case AnchorPosition.Bottom: pt = new PointF(pt.X, Bounds.Bottom); break;
                    case AnchorPosition.Left: pt = new PointF(Bounds.Left, pt.Y); break;
                    case AnchorPosition.Right: pt = new PointF(Bounds.Right, pt.Y); break;
                }
                return RotatePoint(pt, GetCenter(), RotationAngle);
            }

            public virtual PointF GetIntersection(PointF targetPoint)
            {
                PointF center = GetCenter();
                PointF localTarget = RotatePoint(targetPoint, center, -RotationAngle);
                
                float dx = localTarget.X - center.X;
                float dy = localTarget.Y - center.Y;

                if (Math.Abs(dx) == 0 && Math.Abs(dy) == 0) return center;

                float halfWidth = Bounds.Width / 2;
                float halfHeight = Bounds.Height / 2;

                float crossX = halfWidth * Math.Sign(dx);
                float crossY = halfHeight * Math.Sign(dy);

                PointF localIntersection;
                if (Math.Abs(dx * halfHeight) > Math.Abs(dy * halfWidth))
                {
                    localIntersection = new PointF(center.X + crossX, center.Y + crossX * dy / dx);
                }
                else
                {
                    localIntersection = new PointF(center.X + crossY * dx / dy, center.Y + crossY);
                }

                return RotatePoint(localIntersection, center, RotationAngle);
            }

            public static PointF RotatePoint(PointF pt, PointF center, float angleDegrees)
            {
                float angleRadians = angleDegrees * (float)Math.PI / 180f;
                float cosTheta = (float)Math.Cos(angleRadians);
                float sinTheta = (float)Math.Sin(angleRadians);
                return new PointF(
                    cosTheta * (pt.X - center.X) - sinTheta * (pt.Y - center.Y) + center.X,
                    sinTheta * (pt.X - center.X) + cosTheta * (pt.Y - center.Y) + center.Y
                );
            }

            public static float Distance(PointF p1, PointF p2)
            {
                return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
            }
        }
    }
}
