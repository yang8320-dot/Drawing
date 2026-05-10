using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DrawingApp
{
    // 負責處理畫布的繪圖引擎 (OnPaint)、尺規、格線與小地圖的渲染
    public partial class App_CanvasControl
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            EnsureQuadTree();
            var oldTransform = g.Transform;

            g.TranslateTransform(_cameraOffset.X, _cameraOffset.Y);
            g.ScaleTransform(ZoomFactor, ZoomFactor);

            RectangleF clipWorldBounds = new RectangleF(
                (e.ClipRectangle.X - _cameraOffset.X) / ZoomFactor,
                (e.ClipRectangle.Y - _cameraOffset.Y) / ZoomFactor,
                e.ClipRectangle.Width / ZoomFactor,
                e.ClipRectangle.Height / ZoomFactor
            );

            SizeF currentCanvasSize = ActualPageSize;

            RectangleF viewRect = new RectangleF(
                -_cameraOffset.X / ZoomFactor, 
                -_cameraOffset.Y / ZoomFactor, 
                this.Width / ZoomFactor, 
                this.Height / ZoomFactor);

            if (SnapToGrid) DrawGrid(g, viewRect);

            using (Pen pPage = new Pen(Color.LightCoral, 2) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pPage, 0, 0, currentCanvasSize.Width, currentCanvasSize.Height);

            List<App_Shapes.ShapeBase> visibleShapes = new List<App_Shapes.ShapeBase>();
            if (_quadTree != null) _quadTree.Retrieve(visibleShapes, clipWorldBounds);

            // 強制包含選取中或暫存的圖形，確保它們在拖曳時不消失
            foreach (var s in SelectedShapes) if (!visibleShapes.Contains(s)) visibleShapes.Add(s);
            if (_tempShape != null && !visibleShapes.Contains(_tempShape)) visibleShapes.Add(_tempShape);

            var sortedVisibleShapes = visibleShapes.Distinct().OrderBy(s => Shapes.IndexOf(s)).ToList();

            // 1. 繪製非連線圖形
            for (int i = 0; i < sortedVisibleShapes.Count; i++)
            {
                var shape = sortedVisibleShapes[i];
                if (!(shape is App_Shapes.ConnectorShape))
                {
                    shape.DrawWithTransform(g);
                }
            }

            // 2. 繪製連線圖形 (包含跳線與避障運算)
            bool isFastMode = _isPanning; // 如果正在平移畫布，切換為快速渲染模式，避免卡頓
            for (int i = 0; i < Shapes.Count; i++)
            {
                if (Shapes[i] is App_Shapes.ConnectorShape shape)
                {
                    App_Shapes.ShapeBase src = null, tgt = null;
                    for (int j = 0; j < Shapes.Count; j++)
                    {
                        if (Shapes[j].Id == shape.SourceId) src = Shapes[j];
                        if (Shapes[j].Id == shape.TargetId) tgt = Shapes[j];
                        if (src != null && tgt != null) break;
                    }

                    PointF p1 = shape.StartPt, p2 = shape.EndPt;

                    if (src != null)
                        p1 = shape.SourceAnchor == App_Shapes.AnchorPosition.Auto ? src.GetIntersection(tgt != null ? tgt.GetCenter() : shape.EndPt) : src.GetAnchorPoint(shape.SourceAnchor);
                    
                    if (tgt != null)
                        p2 = shape.TargetAnchor == App_Shapes.AnchorPosition.Auto ? tgt.GetIntersection(p1) : tgt.GetAnchorPoint(shape.TargetAnchor);

                    shape.DrawDynamic(g, p1, p2, Shapes, isFastMode, _quadTree);
                }
            }

            // 3. 繪製暫存圖形
            _tempShape?.DrawWithTransform(g);
            if (_tempShape is App_Shapes.ConnectorShape tc) tc.DrawDynamic(g, tc.StartPt, tc.EndPt, Shapes, true, _quadTree); 
            
            // 4. 交給 Tool 繪製額外的 UI (例如懸停導引線、框選框)
            _currentToolInstance?.OnPaint(this, g);

            // 5. 繪製連線吸附提示紅點
            if (_hoveredShapeForConnection != null)
            {
                PointF anchorPt = _hoveredAnchor == App_Shapes.AnchorPosition.Auto 
                    ? _hoveredShapeForConnection.GetIntersection(GetRealPointFromMouse()) 
                    : _hoveredShapeForConnection.GetAnchorPoint(_hoveredAnchor);
                
                g.FillEllipse(Brushes.LightCoral, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
                g.DrawEllipse(Pens.Red, anchorPt.X - 5, anchorPt.Y - 5, 10, 10);
            }

            // 6. 繪製選取框
            for (int i = 0; i < SelectedShapes.Count; i++) SelectedShapes[i].DrawSelection(g);

            // 7. 繪製智慧導引線
            using (Pen guidePen = new Pen(Color.DeepPink, 1.5f) { DashStyle = DashStyle.Dash })
            {
                for (int i = 0; i < _smartGuides.Count; i++) g.DrawLine(guidePen, _smartGuides[i].Item1, _smartGuides[i].Item2);
            }

            g.Transform = oldTransform; 

            // 8. 繪製小地圖與尺規
            DrawMinimap(g, currentCanvasSize);
            if (ShowRulers) DrawRulers(g);
        }

        private void DrawGrid(Graphics g, RectangleF viewRect)
        {
            int startX = (int)(Math.Floor(viewRect.Left / GridSize) * GridSize);
            int startY = (int)(Math.Floor(viewRect.Top / GridSize) * GridSize);
            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235)))
            {
                for (float x = startX; x < viewRect.Right; x += GridSize) g.DrawLine(gridPen, x, viewRect.Top, x, viewRect.Bottom);
                for (float y = startY; y < viewRect.Bottom; y += GridSize) g.DrawLine(gridPen, viewRect.Left, y, viewRect.Right, y);
            }
        }

        private void DrawRulers(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.None; 
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (Brush rulerBg = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (Pen rulerPen = new Pen(Color.FromArgb(180, 180, 180)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(100, 100, 100)))
            using (Font rulerFont = new Font("Arial", 7))
            {
                g.FillRectangle(rulerBg, 0, 0, this.Width, RULER_SIZE); g.DrawLine(rulerPen, 0, RULER_SIZE, this.Width, RULER_SIZE);
                g.FillRectangle(rulerBg, 0, 0, RULER_SIZE, this.Height); g.DrawLine(rulerPen, RULER_SIZE, 0, RULER_SIZE, this.Height);
                g.FillRectangle(Brushes.White, 0, 0, RULER_SIZE, RULER_SIZE); g.DrawRectangle(rulerPen, 0, 0, RULER_SIZE, RULER_SIZE);

                float step = 100 * ZoomFactor, subStep = step / 10;
                float startX = _cameraOffset.X % step, worldStartX = -((int)(_cameraOffset.X / step)) * 100;
                if (startX > 0) { startX -= step; worldStartX -= 100; }

                for (float x = startX; x < this.Width; x += step)
                {
                    if (x > RULER_SIZE) { g.DrawLine(rulerPen, x, 0, x, RULER_SIZE); g.DrawString(worldStartX.ToString(), rulerFont, textBrush, x + 2, 2); }
                    for (int i = 1; i < 10; i++) {
                        float subX = x + i * subStep;
                        if (subX > RULER_SIZE) { int lineLen = (i == 5) ? 10 : 5; g.DrawLine(rulerPen, subX, RULER_SIZE - lineLen, subX, RULER_SIZE); }
                    }
                    worldStartX += 100;
                }

                float startY = _cameraOffset.Y % step, worldStartY = -((int)(_cameraOffset.Y / step)) * 100;
                if (startY > 0) { startY -= step; worldStartY -= 100; }
                StringFormat sfVert = new StringFormat() { FormatFlags = StringFormatFlags.DirectionVertical };

                for (float y = startY; y < this.Height; y += step)
                {
                    if (y > RULER_SIZE) { g.DrawLine(rulerPen, 0, y, RULER_SIZE, y); g.DrawString(worldStartY.ToString(), rulerFont, textBrush, 2, y + 2, sfVert); }
                    for (int i = 1; i < 10; i++) {
                        float subY = y + i * subStep;
                        if (subY > RULER_SIZE) { int lineLen = (i == 5) ? 10 : 5; g.DrawLine(rulerPen, RULER_SIZE - lineLen, subY, RULER_SIZE, subY); }
                    }
                    worldStartY += 100;
                }

                if (_currentMouseScreenPos != Point.Empty)
                {
                    using (Pen cursorPen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
                    {
                        g.DrawLine(cursorPen, _currentMouseScreenPos.X, 0, _currentMouseScreenPos.X, RULER_SIZE);
                        g.DrawLine(cursorPen, 0, _currentMouseScreenPos.Y, RULER_SIZE, _currentMouseScreenPos.Y);
                    }
                }
            }
        }

        private void DrawMinimap(Graphics g, SizeF currentCanvasSize)
        {
            float minimapScale = MINIMAP_WIDTH / currentCanvasSize.Width;
            int minimapHeight = (int)(currentCanvasSize.Height * minimapScale);
            _minimapRect = new Rectangle(this.Width - MINIMAP_WIDTH - 20, this.Height - minimapHeight - 20, MINIMAP_WIDTH, minimapHeight);

            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 245, 245, 245))) g.FillRectangle(bgBrush, _minimapRect);
            g.DrawRectangle(Pens.Gray, _minimapRect);

            for (int i = 0; i < Shapes.Count; i++)
            {
                var shape = Shapes[i];
                if (!(shape is App_Shapes.ConnectorShape))
                {
                    float sx = _minimapRect.X + shape.Bounds.X * minimapScale, sy = _minimapRect.Y + shape.Bounds.Y * minimapScale;
                    float sw = shape.Bounds.Width * minimapScale, sh = shape.Bounds.Height * minimapScale;
                    Color renderColor = (shape.FillColor != Color.Transparent) ? shape.FillColor : shape.ShapeColor;
                    using (Brush b = new SolidBrush(renderColor)) g.FillRectangle(b, sx, sy, sw, sh);
                }
            }

            float vx = _minimapRect.X + (-_cameraOffset.X / ZoomFactor) * minimapScale, vy = _minimapRect.Y + (-_cameraOffset.Y / ZoomFactor) * minimapScale;
            float vw = (this.Width / ZoomFactor) * minimapScale, vh = (this.Height / ZoomFactor) * minimapScale;
            vx = Math.Max(_minimapRect.Left, Math.Min(vx, _minimapRect.Right - vw));
            vy = Math.Max(_minimapRect.Top, Math.Min(vy, _minimapRect.Bottom - vh));
            using (Pen vp = new Pen(Color.Red, 2f)) g.DrawRectangle(vp, vx, vy, vw, vh);
        }

        private void UpdateCameraFromMinimap(Point mouseLoc)
        {
            float minimapScale = MINIMAP_WIDTH / ActualPageSize.Width;
            float targetX = (mouseLoc.X - _minimapRect.X) / minimapScale;
            float targetY = (mouseLoc.Y - _minimapRect.Y) / minimapScale;
            _cameraOffset.X = -(targetX * ZoomFactor - this.Width / 2f);
            _cameraOffset.Y = -(targetY * ZoomFactor - this.Height / 2f);
            this.Invalidate();
        }

        public Bitmap GetTransparentCanvasRender()
        {
            ClearSelection();
            float maxX = ActualPageSize.Width, maxY = ActualPageSize.Height;
            Bitmap bmp = new Bitmap(Math.Max((int)maxX + 50, (int)PageSize.Width), Math.Max((int)maxY + 50, (int)PageSize.Height)); 
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < Shapes.Count; i++) Shapes[i].DrawWithTransform(g);
            }
            return bmp;
        }
    }
}
