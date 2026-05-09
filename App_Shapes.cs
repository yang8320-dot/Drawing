using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_Shapes
    {
        // --- 新增 BezierPen 類型 ---
        public enum ShapeType { Pointer, HandPan, FormatPainter, ArrowLine, StraightLine, OrthogonalLine, Rectangle, RoundedRectangle, Circle, Arc, Diamond, Triangle, Pentagon, Hexagon, Star, Cloud, TextNode, Text, Image, Freehand, BezierPen }
        public enum HandlePosition { None, NW, N, NE, W, E, SW, S, SE, Rotate, StartPoint, EndPoint }
        public enum AnchorPosition { Auto, Top, Bottom, Left, Right }
        public enum TextAlign { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }
        
        public enum BrushType { Solid, LinearGradient }

        // --- 空間分割四叉樹 (QuadTree) ---
        public class QuadTree
        {
            private const int MAX_OBJECTS = 10;
            private const int MAX_LEVELS = 5;

            private int _level;
            private List<ShapeBase> _objects;
            public RectangleF Bounds { get; private set; } 
            private QuadTree[] _nodes;

            public QuadTree(int level, RectangleF bounds)
            {
                _level = level;
                _objects = new List<ShapeBase>();
                Bounds = bounds;
                _nodes = new QuadTree[4];
            }

            public void Clear()
            {
                _objects.Clear();
                for (int i = 0; i < _nodes.Length; i++)
                {
                    if (_nodes[i] != null)
                    {
                        _nodes[i].Clear();
                        _nodes[i] = null;
                    }
                }
            }

            private void Split()
            {
                float subWidth = Bounds.Width / 2f;
                float subHeight = Bounds.Height / 2f;
                float x = Bounds.X;
                float y = Bounds.Y;

                _nodes[0] = new QuadTree(_level + 1, new RectangleF(x + subWidth, y, subWidth, subHeight));
                _nodes[1] = new QuadTree(_level + 1, new RectangleF(x, y, subWidth, subHeight));
                _nodes[2] = new QuadTree(_level + 1, new RectangleF(x, y + subHeight, subWidth, subHeight));
                _nodes[3] = new QuadTree(_level + 1, new RectangleF(x + subWidth, y + subHeight, subWidth, subHeight));
            }

            private int GetIndex(RectangleF pRect)
            {
                int index = -1;
                double verticalMidpoint = Bounds.X + (Bounds.Width / 2f);
                double horizontalMidpoint = Bounds.Y + (Bounds.Height / 2f);

                bool topQuadrant = (pRect.Y < horizontalMidpoint && pRect.Y + pRect.Height < horizontalMidpoint);
                bool bottomQuadrant = (pRect.Y > horizontalMidpoint);

                if (pRect.X < verticalMidpoint && pRect.X + pRect.Width < verticalMidpoint)
                {
                    if (topQuadrant) index = 1;
                    else if (bottomQuadrant) index = 2;
                }
                else if (pRect.X > verticalMidpoint)
                {
                    if (topQuadrant) index = 0;
                    else if (bottomQuadrant) index = 3;
                }
                return index;
            }

            public void Insert(ShapeBase shape)
            {
                if (_nodes[0] != null)
                {
                    int index = GetIndex(shape.Bounds);
                    if (index != -1)
                    {
                        _nodes[index].Insert(shape);
                        return;
                    }
                }

                _objects.Add(shape);

                if (_objects.Count > MAX_OBJECTS && _level < MAX_LEVELS)
                {
                    if (_nodes[0] == null) Split();

                    int i = 0;
                    while (i < _objects.Count)
                    {
                        int index = GetIndex(_objects[i].Bounds);
                        if (index != -1)
                        {
                            _nodes[index].Insert(_objects[i]);
                            _objects.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }

            public List<ShapeBase> Retrieve(List<ShapeBase> returnObjects, RectangleF pRect)
            {
                int index = GetIndex(pRect);
                if (index != -1 && _nodes[0] != null)
                {
                    _nodes[index].Retrieve(returnObjects, pRect);
                }
                else if (_nodes[0] != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (_nodes[i].Bounds.IntersectsWith(pRect))
                        {
                            _nodes[i].Retrieve(returnObjects, pRect);
                        }
                    }
                }

                returnObjects.AddRange(_objects);
                return returnObjects;
            }
        }
        // --------------------------------------------------------

        public abstract class ShapeBase : IDisposable
        {
            protected static readonly Brush SharedShadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

            protected Pen _cachedPen;
            protected Brush _cachedFillBrush;
            protected Font _cachedFont;
            protected Brush _cachedTextBrush;
            private RectangleF _lastBrushBounds;

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
                    if (FillBrushType == BrushType.Solid || rect.Width <= 0 || rect.Height <= 0)
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

        // --- 新增：鋼筆工具 (Bezier Pen) ---
        public class BezierNode
        {
            public PointF Anchor { get; set; }
            public PointF Control1 { get; set; }
            public PointF Control2 { get; set; }
            public BezierNode(PointF pt) { Anchor = pt; Control1 = pt; Control2 = pt; }
        }

        public class BezierShape : ShapeBase
        {
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)]
            public List<BezierNode> LocalNodes { get; set; } = new List<BezierNode>();

            private Pen _cachedShadowPen;

            public BezierShape() { }

            public BezierShape(PointF start, Color color) : base(start, color)
            {
                LocalNodes.Add(new BezierNode(new PointF(0, 0)));
            }

            public void AddNode(PointF absolutePt)
            {
                LocalNodes.Add(new BezierNode(new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y)));
            }

            public void UpdateLastControlPoint(PointF absoluteDragPt)
            {
                if (LocalNodes.Count == 0) return;
                var lastNode = LocalNodes.Last();
                PointF localDrag = new PointF(absoluteDragPt.X - Bounds.X, absoluteDragPt.Y - Bounds.Y);
                lastNode.Control2 = localDrag;
                // 對稱控制桿
                lastNode.Control1 = new PointF(lastNode.Anchor.X - (localDrag.X - lastNode.Anchor.X), lastNode.Anchor.Y - (localDrag.Y - lastNode.Anchor.Y));
            }

            public void UpdateLastAnchorPoint(PointF absolutePt)
            {
                if (LocalNodes.Count == 0) return;
                var lastNode = LocalNodes.Last();
                PointF localPt = new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y);
                lastNode.Anchor = localPt;
                lastNode.Control1 = localPt;
                lastNode.Control2 = localPt;
            }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedShadowPen != null) { _cachedShadowPen.Dispose(); _cachedShadowPen = null; }
            }

            private GraphicsPath GetPath()
            {
                GraphicsPath path = new GraphicsPath();
                if (LocalNodes.Count < 2) return path;

                List<PointF> pts = new List<PointF>();
                pts.Add(new PointF(Bounds.X + LocalNodes[0].Anchor.X, Bounds.Y + LocalNodes[0].Anchor.Y));

                for (int i = 1; i < LocalNodes.Count; i++)
                {
                    pts.Add(new PointF(Bounds.X + LocalNodes[i - 1].Control2.X, Bounds.Y + LocalNodes[i - 1].Control2.Y));
                    pts.Add(new PointF(Bounds.X + LocalNodes[i].Control1.X, Bounds.Y + LocalNodes[i].Control1.Y));
                    pts.Add(new PointF(Bounds.X + LocalNodes[i].Anchor.X, Bounds.Y + LocalNodes[i].Anchor.Y));
                }

                if (pts.Count >= 4)
                {
                    path.AddBeziers(pts.ToArray());
                }
                return path;
            }

            public override void Draw(Graphics g)
            {
                if (LocalNodes.Count < 2) return;

                using (GraphicsPath path = GetPath())
                {
                    if (EnableShadow)
                    {
                        if (_cachedShadowPen == null)
                            _cachedShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), StrokeWidth) { LineJoin = LineJoin.Round };
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.DrawPath(_cachedShadowPen, path);
                        if (FillColor != Color.Transparent) g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent) g.FillPath(GetCachedFillBrush(Bounds), path);
                    g.DrawPath(GetCachedPen(), path);
                }
            }

            public override void DrawSelection(Graphics g)
            {
                base.DrawSelection(g);

                if (!IsSelected || IsLocked || LocalNodes.Count < 1) return;

                Matrix oldMatrix = g.Transform;
                PointF center = GetCenter();
                g.TranslateTransform(center.X, center.Y);
                g.RotateTransform(RotationAngle);
                g.TranslateTransform(-center.X, -center.Y);

                using (Pen handlePen = new Pen(Color.CornflowerBlue, 1) { DashStyle = DashStyle.Dot })
                {
                    foreach (var node in LocalNodes)
                    {
                        PointF absA = new PointF(Bounds.X + node.Anchor.X, Bounds.Y + node.Anchor.Y);
                        PointF absC1 = new PointF(Bounds.X + node.Control1.X, Bounds.Y + node.Control1.Y);
                        PointF absC2 = new PointF(Bounds.X + node.Control2.X, Bounds.Y + node.Control2.Y);

                        g.DrawLine(handlePen, absC1, absA);
                        g.DrawLine(handlePen, absA, absC2);

                        g.FillEllipse(Brushes.White, absA.X - 3, absA.Y - 3, 6, 6);
                        g.DrawEllipse(Pens.Blue, absA.X - 3, absA.Y - 3, 6, 6);

                        g.FillRectangle(Brushes.White, absC1.X - 2, absC1.Y - 2, 4, 4);
                        g.DrawRectangle(Pens.Gray, absC1.X - 2, absC1.Y - 2, 4, 4);
                        
                        g.FillRectangle(Brushes.White, absC2.X - 2, absC2.Y - 2, 4, 4);
                        g.DrawRectangle(Pens.Gray, absC2.X - 2, absC2.Y - 2, 4, 4);
                    }
                }

                g.Transform = oldMatrix;
            }

            public override void NormalizeBounds()
            {
                if (LocalNodes.Count == 0) return;

                float minX = LocalNodes.Min(n => Math.Min(n.Anchor.X, Math.Min(n.Control1.X, n.Control2.X)));
                float minY = LocalNodes.Min(n => Math.Min(n.Anchor.Y, Math.Min(n.Control1.Y, n.Control2.Y)));
                float maxX = LocalNodes.Max(n => Math.Max(n.Anchor.X, Math.Max(n.Control1.X, n.Control2.X)));
                float maxY = LocalNodes.Max(n => Math.Max(n.Anchor.Y, Math.Max(n.Control1.Y, n.Control2.Y)));

                float newWidth = maxX - minX;
                float newHeight = maxY - minY;

                float absMinX = Bounds.X + minX;
                float absMinY = Bounds.Y + minY;

                Bounds = new RectangleF(absMinX, absMinY, newWidth, newHeight);

                foreach (var node in LocalNodes)
                {
                    node.Anchor = new PointF(node.Anchor.X - minX, node.Anchor.Y - minY);
                    node.Control1 = new PointF(node.Control1.X - minX, node.Control1.Y - minY);
                    node.Control2 = new PointF(node.Control2.X - minX, node.Control2.Y - minY);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;

                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                foreach (var node in LocalNodes)
                {
                    node.Anchor = new PointF(node.Anchor.X * scaleX, node.Anchor.Y * scaleY);
                    node.Control1 = new PointF(node.Control1.X * scaleX, node.Control1.Y * scaleY);
                    node.Control2 = new PointF(node.Control2.X * scaleX, node.Control2.Y * scaleY);
                }
                base.SetBounds(newBounds);
            }
        }
        // ------------------------------------

        public class GroupShape : ShapeBase
        {
            [Browsable(false)]
            public List<ShapeBase> Children { get; set; } = new List<ShapeBase>();

            public GroupShape() { }

            public GroupShape(List<ShapeBase> children)
            {
                Children = children;
                NormalizeBounds();
            }

            public override void Dispose()
            {
                base.Dispose();
                foreach (var child in Children) child.Dispose();
            }

            public override void Draw(Graphics g)
            {
                foreach (var child in Children)
                {
                    child.DrawWithTransform(g);
                }
            }

            public override void Move(float dx, float dy)
            {
                if (IsLocked) return;
                base.Move(dx, dy);
                foreach (var child in Children)
                {
                    child.Move(dx, dy);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;
                
                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                foreach (var child in Children)
                {
                    float newChildX = newBounds.X + (child.Bounds.X - Bounds.X) * scaleX;
                    float newChildY = newBounds.Y + (child.Bounds.Y - Bounds.Y) * scaleY;
                    float newChildW = child.Bounds.Width * scaleX;
                    float newChildH = child.Bounds.Height * scaleY;
                    child.SetBounds(new RectangleF(newChildX, newChildY, newChildW, newChildH));
                }
                base.SetBounds(newBounds);
            }

            public override void NormalizeBounds()
            {
                if (Children.Count == 0) return;
                
                float minX = Children.Min(c => c.Bounds.Left);
                float minY = Children.Min(c => c.Bounds.Top);
                float maxX = Children.Max(c => c.Bounds.Right);
                float maxY = Children.Max(c => c.Bounds.Bottom);
                
                Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            }

            public override bool HitTest(PointF pt)
            {
                return Children.Any(c => c.HitTest(pt));
            }
        }

        public class FreehandShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;

            [Browsable(false)]
            public List<PointF> LocalPoints { get; set; } = new List<PointF>();

            private Pen _cachedFreehandShadowPen;

            public FreehandShape() { }

            public FreehandShape(PointF start, Color color) : base(start, color)
            {
                LocalPoints.Add(new PointF(0, 0));
            }

            public void AddPoint(PointF absolutePt)
            {
                LocalPoints.Add(new PointF(absolutePt.X - Bounds.X, absolutePt.Y - Bounds.Y));
            }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedFreehandShadowPen != null) { _cachedFreehandShadowPen.Dispose(); _cachedFreehandShadowPen = null; }
            }

            public override void Draw(Graphics g)
            {
                if (LocalPoints.Count > 1)
                {
                    Pen p = GetCachedPen();
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    PointF[] absPts = LocalPoints.Select(pt => new PointF(Bounds.X + pt.X, Bounds.Y + pt.Y)).ToArray();
                    
                    if (EnableShadow)
                    {
                        if (_cachedFreehandShadowPen == null)
                            _cachedFreehandShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), p.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                        
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        if (absPts.Length > 2) g.DrawCurve(_cachedFreehandShadowPen, absPts); 
                        else g.DrawLines(_cachedFreehandShadowPen, absPts);
                        g.Transform = m;
                    }

                    if (absPts.Length > 2)
                        g.DrawCurve(p, absPts); 
                    else
                        g.DrawLines(p, absPts);
                }
            }

            public override void NormalizeBounds()
            {
                if (LocalPoints.Count == 0) return;
                float minX = LocalPoints.Min(p => p.X);
                float minY = LocalPoints.Min(p => p.Y);
                float maxX = LocalPoints.Max(p => p.X);
                float maxY = LocalPoints.Max(p => p.Y);

                float newWidth = maxX - minX;
                float newHeight = maxY - minY;

                float absMinX = Bounds.X + minX;
                float absMinY = Bounds.Y + minY;

                Bounds = new RectangleF(absMinX, absMinY, newWidth, newHeight);

                for (int i = 0; i < LocalPoints.Count; i++)
                {
                    LocalPoints[i] = new PointF(LocalPoints[i].X - minX, LocalPoints[i].Y - minY);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;

                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                for (int i = 0; i < LocalPoints.Count; i++)
                {
                    LocalPoints[i] = new PointF(LocalPoints[i].X * scaleX, LocalPoints[i].Y * scaleY);
                }
                base.SetBounds(newBounds);
            }
        }

        public class RectShape : ShapeBase
        { 
            public RectShape() { } 
            public RectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if (EnableShadow)
                    g.FillRectangle(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                
                if (FillColor != Color.Transparent)
                    g.FillRectangle(GetCachedFillBrush(Bounds), Bounds);
                
                g.DrawRectangle(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawText(g);
            }
        }

        public class RoundedRectShape : ShapeBase
        {
            public RoundedRectShape() { }
            public RoundedRectShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                float radius = Math.Min(Bounds.Width, Bounds.Height) * 0.2f;
                if (radius <= 0) return;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(Bounds.X, Bounds.Y, radius * 2, radius * 2, 180, 90);
                    path.AddArc(Bounds.Right - radius * 2, Bounds.Y, radius * 2, radius * 2, 270, 90);
                    path.AddArc(Bounds.Right - radius * 2, Bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                    path.AddArc(Bounds.X, Bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                    path.CloseFigure();

                    if (EnableShadow)
                    {
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent)
                        g.FillPath(GetCachedFillBrush(Bounds), path);
                    
                    g.DrawPath(GetCachedPen(), path);
                }
                DrawText(g);
            }
        }
        
        public class CircleShape : ShapeBase
        {
            public CircleShape() { } 
            public CircleShape(PointF start, Color color) : base(start, color) { }
            public override void Draw(Graphics g)
            {
                if (EnableShadow)
                    g.FillEllipse(SharedShadowBrush, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height);
                
                if (FillColor != Color.Transparent)
                    g.FillEllipse(GetCachedFillBrush(Bounds), Bounds);
                
                g.DrawEllipse(GetCachedPen(), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
                DrawText(g);
            }

            public override PointF GetIntersection(PointF targetPoint)
            {
                PointF center = GetCenter();
                float dx = targetPoint.X - center.X;
                float dy = targetPoint.Y - center.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (distance == 0) return center;

                float radiusX = Bounds.Width / 2;
                float radiusY = Bounds.Height / 2;
                float radius = Math.Min(radiusX, radiusY);

                return new PointF(center.X + (dx / distance) * radius, center.Y + (dy / distance) * radius);
            }
        }

        public class ArcShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            
            private Pen _cachedArcShadowPen;

            public ArcShape() { } 
            public ArcShape(PointF start, Color color) : base(start, color) { }

            protected override void InvalidatePen()
            {
                base.InvalidatePen();
                if (_cachedArcShadowPen != null) { _cachedArcShadowPen.Dispose(); _cachedArcShadowPen = null; }
            }

            public override void Draw(Graphics g)
            {
                if(Bounds.Width > 0 && Bounds.Height > 0)
                {
                    if (EnableShadow)
                    {
                        if (_cachedArcShadowPen == null)
                            _cachedArcShadowPen = new Pen(Color.FromArgb(60, 0, 0, 0), StrokeWidth);
                        g.DrawArc(_cachedArcShadowPen, Bounds.X + 6, Bounds.Y + 6, Bounds.Width, Bounds.Height, 180, 180);
                    }
                    g.DrawArc(GetCachedPen(), Bounds, 180, 180);
                }
                DrawText(g);
            }
        }

        public class DiamondShape : ShapeBase
        {
            public DiamondShape() { }
            public DiamondShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                return new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Y + Bounds.Height / 2),
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Y + Bounds.Height / 2)
                };
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (EnableShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class TriangleShape : ShapeBase
        {
            public TriangleShape() { }
            public TriangleShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                return new PointF[]
                {
                    new PointF(Bounds.X + Bounds.Width / 2, Bounds.Y),
                    new PointF(Bounds.Right, Bounds.Bottom),
                    new PointF(Bounds.X, Bounds.Bottom)
                };
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (EnableShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class PentagonShape : ShapeBase
        {
            public PentagonShape() { }
            public PentagonShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float rx = Bounds.Width / 2f;
                float ry = Bounds.Height / 2f;
                PointF[] pts = new PointF[5];
                for (int i = 0; i < 5; i++)
                {
                    double angle = Math.PI / 2 + (i * 2 * Math.PI / 5);
                    pts[i] = new PointF(center.X - (float)(rx * Math.Cos(angle)), center.Y - (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (EnableShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class HexagonShape : ShapeBase
        {
            public HexagonShape() { }
            public HexagonShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float rx = Bounds.Width / 2f;
                float ry = Bounds.Height / 2f;
                PointF[] pts = new PointF[6];
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3;
                    pts[i] = new PointF(center.X + (float)(rx * Math.Cos(angle)), center.Y + (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (EnableShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class StarShape : ShapeBase
        {
            public StarShape() { }
            public StarShape(PointF start, Color color) : base(start, color) { }

            public PointF[] GetPolygonPoints()
            {
                PointF center = GetCenter();
                float outerRx = Bounds.Width / 2f;
                float outerRy = Bounds.Height / 2f;
                float innerRx = outerRx * 0.4f;
                float innerRy = outerRy * 0.4f;
                PointF[] pts = new PointF[10];

                for (int i = 0; i < 10; i++)
                {
                    double angle = Math.PI / 2 + (i * Math.PI / 5);
                    float rx = (i % 2 == 0) ? outerRx : innerRx;
                    float ry = (i % 2 == 0) ? outerRy : innerRy;
                    pts[i] = new PointF(center.X - (float)(rx * Math.Cos(angle)), center.Y - (float)(ry * Math.Sin(angle)));
                }
                return pts;
            }

            public override void Draw(Graphics g)
            {
                PointF[] pts = GetPolygonPoints();
                if (EnableShadow)
                {
                    var m = g.Transform.Clone();
                    g.TranslateTransform(6, 6);
                    g.FillPolygon(SharedShadowBrush, pts);
                    g.Transform = m;
                }
                if (FillColor != Color.Transparent)
                    g.FillPolygon(GetCachedFillBrush(Bounds), pts);
                
                g.DrawPolygon(GetCachedPen(), pts);
                DrawText(g);
            }
        }

        public class CloudShape : ShapeBase
        {
            public CloudShape() { }
            public CloudShape(PointF start, Color color) : base(start, color) { }

            public override void Draw(Graphics g)
            {
                if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.FillMode = FillMode.Winding;
                    
                    float x = Bounds.X, y = Bounds.Y, w = Bounds.Width, h = Bounds.Height;
                    path.AddEllipse(x + w * 0.15f, y + h * 0.2f, w * 0.4f, h * 0.5f);
                    path.AddEllipse(x + w * 0.35f, y + h * 0.1f, w * 0.5f, h * 0.6f);
                    path.AddEllipse(x + w * 0.55f, y + h * 0.3f, w * 0.35f, h * 0.5f);
                    path.AddEllipse(x + w * 0.25f, y + h * 0.4f, w * 0.5f, h * 0.5f);

                    if (EnableShadow)
                    {
                        var m = g.Transform.Clone();
                        g.TranslateTransform(6, 6);
                        g.FillPath(SharedShadowBrush, path);
                        g.Transform = m;
                    }

                    if (FillColor != Color.Transparent)
                        g.FillPath(GetCachedFillBrush(Bounds), path);
                    
                    g.DrawPath(GetCachedPen(), path);
                }
                DrawText(g);
            }
        }

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

        public class ConnectorShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override float RotationAngle { get; set; } = 0f;
            [Browsable(false)] public override string Text { get; set; } = "";
            [Browsable(false)] public override string FontName { get; set; } = "Arial";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;
            [Browsable(false)] public override TextAlign TextAlignment { get; set; } = TextAlign.MiddleCenter;

            [Browsable(false)] public Guid SourceId { get; set; }
            [Browsable(false)] public Guid TargetId { get; set; }
            
            [Category("5. 連線屬性")]
            [DisplayName("起點錨點")]
            public AnchorPosition SourceAnchor { get; set; } = AnchorPosition.Auto;
            
            [Category("5. 連線屬性")]
            [DisplayName("終點錨點")]
            public AnchorPosition TargetAnchor { get; set; } = AnchorPosition.Auto;

            [Browsable(false)] public PointF StartPt { get; set; }
            [Browsable(false)] public PointF EndPt { get; set; }
            
            private bool _hasArrow;
            [Category("5. 連線屬性")]
            [DisplayName("顯示箭頭")]
            public bool HasArrow 
            { 
                get => _hasArrow; 
                set { if (_hasArrow != value) { _hasArrow = value; InvalidatePen(); } } 
            }
            
            [Category("5. 連線屬性")]
            [DisplayName("直角折線")]
            public bool IsOrthogonal { get; set; }

            [Category("5. 連線屬性")]
            [DisplayName("開啟交錯跳線")]
            [Description("當連線與其他連線交錯時，是否自動繪製跳線半圓弧。")]
            public bool EnableLineJumps { get; set; } = true;

            [JsonIgnore] 
            [Browsable(false)] 
            public PointF[] CachedPath => _cachedPath;
            private PointF[] _cachedPath;

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow, bool orthogonal = false) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow; IsOrthogonal = orthogonal;
            }
            
            public override void UpdateEndPoint(PointF pt) { if (!IsLocked) EndPt = pt; }
            
            public override bool HitTest(PointF pt) 
            { 
                if (_cachedPath == null || _cachedPath.Length < 2) return false;
                for (int i = 0; i < _cachedPath.Length - 1; i++)
                {
                    if (DistancePointToSegment(pt, _cachedPath[i], _cachedPath[i+1]) < 8f) return true;
                }
                return false; 
            } 

            public override HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected || IsLocked || _cachedPath == null || _cachedPath.Length < 2) return HandlePosition.None;

                float s = 10;
                PointF start = _cachedPath[0];
                PointF end = _cachedPath[_cachedPath.Length - 1];

                if (new RectangleF(start.X - s, start.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.StartPoint;
                if (new RectangleF(end.X - s, end.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.EndPoint;

                return HandlePosition.None;
            }

            public override void Move(float dx, float dy)
            {
                if (IsLocked) return;
                StartPt = new PointF(StartPt.X + dx, StartPt.Y + dy);
                EndPt = new PointF(EndPt.X + dx, EndPt.Y + dy);
            }

            private float DistancePointToSegment(PointF pt, PointF p1, PointF p2)
            {
                float l2 = (p1.X - p2.X)*(p1.X - p2.X) + (p1.Y - p2.Y)*(p1.Y - p2.Y);
                if (l2 == 0) return (float)Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                float t = Math.Max(0, Math.Min(1, ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / l2));
                PointF projection = new PointF(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                return (float)Math.Sqrt(Math.Pow(pt.X - projection.X, 2) + Math.Pow(pt.Y - projection.Y, 2));
            }

            private bool GetIntersection(PointF A, PointF B, PointF C, PointF D, out PointF intersection)
            {
                intersection = PointF.Empty;
                float den = (B.X - A.X) * (D.Y - C.Y) - (B.Y - A.Y) * (D.X - C.X);
                if (den == 0) return false;
                
                float num1 = (A.Y - C.Y) * (D.X - C.X) - (A.X - C.X) * (D.Y - C.Y);
                float num2 = (A.Y - C.Y) * (B.X - A.X) - (A.X - C.X) * (B.Y - A.Y);
                
                float r = num1 / den;
                float s = num2 / den;
                
                if (r > 0.05f && r < 0.95f && s > 0.05f && s < 0.95f) 
                {
                    intersection = new PointF(A.X + r * (B.X - A.X), A.Y + r * (B.Y - A.Y));
                    return true;
                }
                return false;
            }

            private PointF[] CalculateOrthogonalPath(PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes, QuadTree quadTree)
            {
                if (Math.Abs(p1.X - p2.X) < 20 && Math.Abs(p1.Y - p2.Y) < 20)
                {
                    return new PointF[] { p1, new PointF(p1.X, p2.Y), p2 };
                }

                List<RectangleF> obstacles = new List<RectangleF>();
                
                float minX = Math.Min(p1.X, p2.X) - 100;
                float minY = Math.Min(p1.Y, p2.Y) - 100;
                float maxX = Math.Max(p1.X, p2.X) + 100;
                float maxY = Math.Max(p1.Y, p2.Y) + 100;
                RectangleF searchArea = new RectangleF(minX, minY, maxX - minX, maxY - minY);

                List<ShapeBase> nearbyShapes = new List<ShapeBase>();
                if (quadTree != null)
                {
                    quadTree.Retrieve(nearbyShapes, searchArea);
                }
                else if (allShapes != null)
                {
                    nearbyShapes = allShapes.ToList();
                }

                foreach (var s in nearbyShapes)
                {
                    if (s is ConnectorShape || s.Id == this.SourceId || s.Id == this.TargetId) continue;
                    RectangleF obs = s.Bounds;
                    obs.Inflate(15, 15);
                    obstacles.Add(obs);
                }

                List<float> xCoords = new List<float> { p1.X, p2.X, p1.X - 30, p1.X + 30, p2.X - 30, p2.X + 30 };
                List<float> yCoords = new List<float> { p1.Y, p2.Y, p1.Y - 30, p1.Y + 30, p2.Y - 30, p2.Y + 30 };

                foreach (var obs in obstacles)
                {
                    xCoords.Add(obs.Left); xCoords.Add(obs.Right);
                    yCoords.Add(obs.Top); yCoords.Add(obs.Bottom);
                }

                xCoords = xCoords.Distinct().OrderBy(x => x).ToList();
                yCoords = yCoords.Distinct().OrderBy(y => y).ToList();

                if (xCoords.Count * yCoords.Count > 1000) 
                {
                    return BasicOrthogonalPath(p1, p2);
                }

                PointF startNode = GetClosestNode(p1, xCoords, yCoords);
                PointF endNode = GetClosestNode(p2, xCoords, yCoords);

                var openSet = new List<PointF> { startNode };
                var cameFrom = new Dictionary<PointF, PointF>();
                var gScore = new Dictionary<PointF, float> { [startNode] = 0 };
                var fScore = new Dictionary<PointF, float> { [startNode] = Heuristic(startNode, endNode) };

                while (openSet.Count > 0)
                {
                    PointF current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();
                    if (current == endNode) return ReconstructPath(cameFrom, current, p1, p2);

                    openSet.Remove(current);

                    foreach (var neighbor in GetNeighbors(current, xCoords, yCoords))
                    {
                        if (LineIntersectsObstacles(current, neighbor, obstacles)) continue;

                        float penalty = 0;
                        if (cameFrom.ContainsKey(current))
                        {
                            PointF prev = cameFrom[current];
                            bool wasHorizontal = Math.Abs(current.Y - prev.Y) < 1f;
                            bool isHorizontal = Math.Abs(neighbor.Y - current.Y) < 1f;
                            if (wasHorizontal != isHorizontal) penalty = 50f; 
                        }

                        float tentativeG = gScore[current] + Heuristic(current, neighbor) + penalty;

                        if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                        {
                            cameFrom[neighbor] = current;
                            gScore[neighbor] = tentativeG;
                            fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, endNode);
                            if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                        }
                    }
                }

                return BasicOrthogonalPath(p1, p2);
            }

            private PointF[] BasicOrthogonalPath(PointF p1, PointF p2)
            {
                float midX = p1.X + (p2.X - p1.X) / 2;
                float midY = p1.Y + (p2.Y - p1.Y) / 2;
                
                if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Y - p1.Y))
                    return new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                else
                    return new PointF[] { p1, new PointF(p1.X, midY), new PointF(p2.X, midY), p2 };
            }

            private PointF GetClosestNode(PointF p, List<float> xCoords, List<float> yCoords)
            {
                float closeX = xCoords.OrderBy(x => Math.Abs(x - p.X)).First();
                float closeY = yCoords.OrderBy(y => Math.Abs(y - p.Y)).First();
                return new PointF(closeX, closeY);
            }

            private float Heuristic(PointF a, PointF b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

            private IEnumerable<PointF> GetNeighbors(PointF p, List<float> xCoords, List<float> yCoords)
            {
                int xi = xCoords.IndexOf(p.X);
                int yi = yCoords.IndexOf(p.Y);
                if (xi > 0) yield return new PointF(xCoords[xi - 1], p.Y);
                if (xi < xCoords.Count - 1) yield return new PointF(xCoords[xi + 1], p.Y);
                if (yi > 0) yield return new PointF(p.X, yCoords[yi - 1]);
                if (yi < yCoords.Count - 1) yield return new PointF(p.X, yCoords[yi + 1]);
            }

            private bool LineIntersectsObstacles(PointF p1, PointF p2, List<RectangleF> obstacles)
            {
                float minX = Math.Min(p1.X, p2.X);
                float maxX = Math.Max(p1.X, p2.X);
                float minY = Math.Min(p1.Y, p2.Y);
                float maxY = Math.Max(p1.Y, p2.Y);

                foreach (var obs in obstacles)
                {
                    if (p1.X == p2.X) 
                    {
                        if (p1.X > obs.Left && p1.X < obs.Right && minY < obs.Bottom && maxY > obs.Top) return true;
                    }
                    else if (p1.Y == p2.Y) 
                    {
                        if (p1.Y > obs.Top && p1.Y < obs.Bottom && minX < obs.Right && maxX > obs.Left) return true;
                    }
                }
                return false;
            }

            private PointF[] ReconstructPath(Dictionary<PointF, PointF> cameFrom, PointF current, PointF start, PointF end)
            {
                List<PointF> path = new List<PointF> { end };
                while (cameFrom.ContainsKey(current))
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Add(start);
                path.Reverse();

                List<PointF> cleanPath = new List<PointF> { path[0] };
                for (int i = 1; i < path.Count - 1; i++)
                {
                    PointF prev = cleanPath.Last();
                    PointF next = path[i + 1];
                    if (prev.X != next.X && prev.Y != next.Y)
                    {
                        cleanPath.Add(path[i]);
                    }
                }
                cleanPath.Add(path.Last());

                return cleanPath.ToArray();
            }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes = null, bool isFastMode = false, QuadTree quadTree = null)
            {
                PointF[] pts;
                if (IsOrthogonal)
                {
                    if (isFastMode) pts = BasicOrthogonalPath(p1, p2);
                    else pts = CalculateOrthogonalPath(p1, p2, allShapes, quadTree);
                }
                else
                {
                    pts = new PointF[] { p1, p2 };
                }
                _cachedPath = pts;

                Pen p = GetCachedPen();
                
                if (HasArrow && p.CustomEndCap == null)
                {
                    GraphicsPath capPath = new GraphicsPath();
                    capPath.AddLine(new PointF(-4, -4), new PointF(0, 0));
                    capPath.AddLine(new PointF(0, 0), new PointF(4, -4));
                    p.CustomEndCap = new CustomLineCap(null, capPath);
                }
                else if (!HasArrow && p.CustomEndCap != null)
                {
                    p.CustomEndCap.Dispose();
                    p.CustomEndCap = null;
                }

                if (EnableLineJumps && allShapes != null && !isFastMode)
                {
                    using (GraphicsPath mainPath = new GraphicsPath())
                    {
                        for (int i = 0; i < pts.Length - 1; i++)
                        {
                            PointF segStart = pts[i];
                            PointF segEnd = pts[i + 1];
                            
                            List<PointF> intersections = new List<PointF>();

                            List<ShapeBase> checkShapes = allShapes.ToList();
                            if (quadTree != null)
                            {
                                checkShapes.Clear();
                                float minX = Math.Min(segStart.X, segEnd.X) - 10;
                                float minY = Math.Min(segStart.Y, segEnd.Y) - 10;
                                float maxX = Math.Max(segStart.X, segEnd.X) + 10;
                                float maxY = Math.Max(segStart.Y, segEnd.Y) + 10;
                                quadTree.Retrieve(checkShapes, new RectangleF(minX, minY, maxX - minX, maxY - minY));
                            }

                            foreach (var other in checkShapes)
                            {
                                if (other is ConnectorShape otherConn && otherConn != this && otherConn.CachedPath != null && other.Id.CompareTo(this.Id) < 0)
                                {
                                    for (int j = 0; j < otherConn.CachedPath.Length - 1; j++)
                                    {
                                        if (GetIntersection(segStart, segEnd, otherConn.CachedPath[j], otherConn.CachedPath[j + 1], out PointF intersectPt))
                                        {
                                            intersections.Add(intersectPt);
                                        }
                                    }
                                }
                            }

                            if (intersections.Count == 0)
                            {
                                mainPath.AddLine(segStart, segEnd);
                            }
                            else
                            {
                                intersections = intersections.OrderBy(pt => Distance(segStart, pt)).ToList();
                                
                                PointF currentPt = segStart;
                                float jumpRadius = 6f;

                                foreach (var ipt in intersections)
                                {
                                    if (Distance(currentPt, ipt) > jumpRadius)
                                    {
                                        float ratio = (Distance(segStart, ipt) - jumpRadius) / Distance(segStart, segEnd);
                                        PointF preJump = new PointF(segStart.X + ratio * (segEnd.X - segStart.X), segStart.Y + ratio * (segEnd.Y - segStart.Y));
                                        mainPath.AddLine(currentPt, preJump);
                                        
                                        bool isHorizontal = Math.Abs(segStart.Y - segEnd.Y) < 1f;
                                        if (isHorizontal)
                                        {
                                            float sweep = segStart.X < segEnd.X ? 180 : -180;
                                            mainPath.AddArc(ipt.X - jumpRadius, ipt.Y - jumpRadius, jumpRadius * 2, jumpRadius * 2, segStart.X < segEnd.X ? 180 : 0, sweep);
                                        }
                                        else
                                        {
                                            float sweep = segStart.Y < segEnd.Y ? 180 : -180;
                                            mainPath.AddArc(ipt.X - jumpRadius, ipt.Y - jumpRadius, jumpRadius * 2, jumpRadius * 2, segStart.Y < segEnd.Y ? 270 : 90, sweep);
                                        }

                                        ratio = (Distance(segStart, ipt) + jumpRadius) / Distance(segStart, segEnd);
                                        currentPt = new PointF(segStart.X + ratio * (segEnd.X - segStart.X), segStart.Y + ratio * (segEnd.Y - segStart.Y));
                                    }
                                }
                                mainPath.AddLine(currentPt, segEnd);
                            }
                        }
                        g.DrawPath(p, mainPath);
                    }
                }
                else
                {
                    if (pts.Length > 2) g.DrawLines(p, pts);
                    else g.DrawLine(p, p1, p2);
                }
            }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2) => DrawDynamic(g, p1, p2, null, false, null);
            public override void Draw(Graphics g) { }

            public override void DrawSelection(Graphics g)
            {
                if (!IsSelected || _cachedPath == null || _cachedPath.Length < 2) return;
                
                Color ptColor = IsLocked ? Color.LightGray : Color.White;
                Color borderColor = IsLocked ? Color.Gray : Color.DodgerBlue;

                using (Brush b = new SolidBrush(ptColor))
                using (Pen p = new Pen(borderColor))
                {
                    foreach (var pt in _cachedPath)
                    {
                        g.FillRectangle(b, pt.X - 3, pt.Y - 3, 6, 6);
                        g.DrawRectangle(p, pt.X - 3, pt.Y - 3, 6, 6);
                    }
                }

                if (!IsLocked)
                {
                    PointF start = _cachedPath[0];
                    PointF end = _cachedPath[_cachedPath.Length - 1];

                    g.FillEllipse(Brushes.Yellow, start.X - 5, start.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, start.X - 5, start.Y - 5, 10, 10);

                    g.FillEllipse(Brushes.Yellow, end.X - 5, end.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, end.X - 5, end.Y - 5, 10, 10);
                }
            }
        }

        public static class ShapeFactory
        {
            public static ShapeBase CreateShape(ShapeType type, PointF start, Color color, Bitmap img = null)
            {
                switch (type)
                {
                    case ShapeType.ArrowLine: return new ConnectorShape(start, color, true, false);
                    case ShapeType.StraightLine: return new ConnectorShape(start, color, false, false);
                    case ShapeType.OrthogonalLine: return new ConnectorShape(start, color, true, true);
                    case ShapeType.Rectangle: return new RectShape(start, color);
                    case ShapeType.RoundedRectangle: return new RoundedRectShape(start, color);
                    case ShapeType.Circle: return new CircleShape(start, color);
                    case ShapeType.Arc: return new ArcShape(start, color);
                    case ShapeType.Diamond: return new DiamondShape(start, color);
                    case ShapeType.Triangle: return new TriangleShape(start, color);
                    case ShapeType.Pentagon: return new PentagonShape(start, color);
                    case ShapeType.Hexagon: return new HexagonShape(start, color);
                    case ShapeType.Star: return new StarShape(start, color);
                    case ShapeType.Cloud: return new CloudShape(start, color);
                    case ShapeType.TextNode: return new TextNodeShape(start, color, false);
                    case ShapeType.Text: return new TextNodeShape(start, color, true);
                    case ShapeType.Image: return new ImageShape(start, img);
                    case ShapeType.Freehand: return new FreehandShape(start, color);
                    // --- 註冊鋼筆工具 ---
                    case ShapeType.BezierPen: return new BezierShape(start, color);
                    default: return null;
                }
            }
        }
    }
}
