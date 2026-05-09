/*
 * 檔案功能：處理匯出 PNG (透明)、PDF 與 SVG 功能
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DrawingApp
{
    public static class App_Export
    {
        public static async Task ExportToPngAsync(Bitmap canvasBitmap, string filePath)
        {
            await Task.Run(() =>
            {
                canvasBitmap.Save(filePath, ImageFormat.Png);
            });
        }

        public static async Task ExportSelectionToPngAsync(List<App_Shapes.ShapeBase> selectedShapes, string filePath)
        {
            if (selectedShapes == null || selectedShapes.Count == 0) return;

            await Task.Run(() =>
            {
                float minX = selectedShapes.Min(s => Math.Min(s.Bounds.Left, s.Bounds.Right));
                float minY = selectedShapes.Min(s => Math.Min(s.Bounds.Top, s.Bounds.Bottom));
                float maxX = selectedShapes.Max(s => Math.Max(s.Bounds.Left, s.Bounds.Right));
                float maxY = selectedShapes.Max(s => Math.Max(s.Bounds.Top, s.Bounds.Bottom));

                int width = (int)(maxX - minX + 40); // 增加 Padding
                int height = (int)(maxY - minY + 40);

                if (width <= 0 || height <= 0) return;

                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(-minX + 20, -minY + 20); // 位移至中心

                        // 暫時取消選取狀態框線，繪製後再恢復
                        var selectionStates = selectedShapes.ToDictionary(s => s, s => s.IsSelected);
                        foreach (var s in selectedShapes) s.IsSelected = false;

                        foreach (var s in selectedShapes) s.DrawWithTransform(g);

                        // 恢復狀態
                        foreach (var s in selectedShapes) s.IsSelected = selectionStates[s];
                    }
                    bmp.Save(filePath, ImageFormat.Png);
                }
            });
        }

        public static async Task ExportToPdfAsync(Bitmap canvasBitmap, string filePath, bool isLandscape)
        {
            await Task.Run(() =>
            {
                PdfDocument document = new PdfDocument();
                PdfPage page = document.AddPage();
                
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                using (MemoryStream ms = new MemoryStream())
                {
                    canvasBitmap.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    XImage image = XImage.FromStream(ms);
                    
                    gfx.DrawImage(image, 0, 0, page.Width, page.Height);
                }
                document.Save(filePath);
            });
        }

        public static async Task ExportToSvgAsync(List<App_Shapes.ShapeBase> shapes, SizeF pageSize, string filePath)
        {
            await Task.Run(() =>
            {
                StringBuilder svg = new StringBuilder();
                svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                svg.AppendLine($"<svg width=\"{pageSize.Width}\" height=\"{pageSize.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

                foreach (var shape in shapes)
                {
                    string strokeHex = ColorTranslator.ToHtml(shape.ShapeColor);
                    string fillHex = shape.FillColor == Color.Transparent ? "none" : ColorTranslator.ToHtml(shape.FillColor);
                    string fontColorHex = ColorTranslator.ToHtml(shape.FontColor);
                    string dashArray = shape.StrokeDashStyle == System.Drawing.Drawing2D.DashStyle.Dash ? "stroke-dasharray=\"5,5\"" : "";
                    
                    float x = shape.Bounds.X;
                    float y = shape.Bounds.Y;
                    float w = shape.Bounds.Width;
                    float h = shape.Bounds.Height;
                    float cx = x + w / 2;
                    float cy = y + h / 2;
                    
                    string transform = shape.RotationAngle != 0 ? $"transform=\"rotate({shape.RotationAngle},{cx},{cy})\"" : "";

                    if (shape is App_Shapes.RectShape)
                    {
                        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.RoundedRectShape)
                    {
                        float r = Math.Min(w, h) * 0.2f;
                        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" rx=\"{r}\" ry=\"{r}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.TextNodeShape tns)
                    {
                        string tnsFill = tns.IsTransparent ? "none" : fillHex;
                        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{tnsFill}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.CircleShape)
                    {
                        svg.AppendLine($"  <ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{w/2}\" ry=\"{h/2}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.ConnectorShape conn)
                    {
                        svg.AppendLine($"  <line x1=\"{conn.StartPt.X}\" y1=\"{conn.StartPt.Y}\" x2=\"{conn.EndPt.X}\" y2=\"{conn.EndPt.Y}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                    }
                    else if (shape is App_Shapes.FreehandShape fh)
                    {
                        if (fh.LocalPoints.Count > 1)
                        {
                            svg.Append($"  <polyline points=\"");
                            foreach (var pt in fh.LocalPoints)
                            {
                                float px = fh.Bounds.X + pt.X;
                                float py = fh.Bounds.Y + pt.Y;
                                svg.Append($"{px},{py} ");
                            }
                            svg.AppendLine($"\" fill=\"none\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" {dashArray} {transform} />");
                        }
                    }
                    // --- 新增：貝茲曲線 (鋼筆工具) 的 SVG 匯出 ---
                    else if (shape is App_Shapes.BezierShape bezier)
                    {
                        if (bezier.LocalNodes.Count > 1)
                        {
                            svg.Append($"  <path d=\"");
                            
                            // 移至起始點 (MoveTo)
                            float startX = bezier.Bounds.X + bezier.LocalNodes[0].Anchor.X;
                            float startY = bezier.Bounds.Y + bezier.LocalNodes[0].Anchor.Y;
                            svg.Append($"M {startX} {startY} ");

                            // 繪製每一段的三次方貝茲曲線 (Cubic Bezier)
                            for (int i = 1; i < bezier.LocalNodes.Count; i++)
                            {
                                float c1x = bezier.Bounds.X + bezier.LocalNodes[i - 1].Control2.X;
                                float c1y = bezier.Bounds.Y + bezier.LocalNodes[i - 1].Control2.Y;
                                
                                float c2x = bezier.Bounds.X + bezier.LocalNodes[i].Control1.X;
                                float c2y = bezier.Bounds.Y + bezier.LocalNodes[i].Control1.Y;
                                
                                float endX = bezier.Bounds.X + bezier.LocalNodes[i].Anchor.X;
                                float endY = bezier.Bounds.Y + bezier.LocalNodes[i].Anchor.Y;

                                svg.Append($"C {c1x} {c1y}, {c2x} {c2y}, {endX} {endY} ");
                            }
                            
                            svg.AppendLine($"\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" {dashArray} {transform} />");
                        }
                    }
                    else if (shape is App_Shapes.TriangleShape || shape is App_Shapes.DiamondShape || 
                             shape is App_Shapes.StarShape || shape is App_Shapes.PentagonShape || shape is App_Shapes.HexagonShape)
                    {
                        PointF[] pts = null;

                        if (shape is App_Shapes.TriangleShape ts) pts = ts.GetPolygonPoints();
                        else if (shape is App_Shapes.DiamondShape ds) pts = ds.GetPolygonPoints();
                        else if (shape is App_Shapes.StarShape ss) pts = ss.GetPolygonPoints();
                        else if (shape is App_Shapes.PentagonShape ps) pts = ps.GetPolygonPoints();
                        else if (shape is App_Shapes.HexagonShape hs) pts = hs.GetPolygonPoints();

                        if (pts != null)
                        {
                            svg.Append($"  <polygon points=\"");
                            foreach (var pt in pts) svg.Append($"{pt.X},{pt.Y} ");
                            svg.AppendLine($"\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                        }
                    }
                    else if (shape is App_Shapes.CloudShape)
                    {
                        svg.AppendLine($"  <g {transform}>");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.35f}\" cy=\"{y + h * 0.45f}\" rx=\"{w * 0.2f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.60f}\" cy=\"{y + h * 0.40f}\" rx=\"{w * 0.25f}\" ry=\"{h * 0.30f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.75f}\" cy=\"{y + h * 0.60f}\" rx=\"{w * 0.17f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.50f}\" cy=\"{y + h * 0.65f}\" rx=\"{w * 0.25f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.25f}\" cy=\"{y + h * 0.60f}\" rx=\"{w * 0.15f}\" ry=\"{h * 0.20f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        
                        if (shape.FillColor != Color.Transparent) {
                            svg.AppendLine($"    <ellipse cx=\"{x + w * 0.5f}\" cy=\"{y + h * 0.55f}\" rx=\"{w * 0.3f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"none\" />");
                        }
                        svg.AppendLine($"  </g>");
                    }

                    if (!string.IsNullOrEmpty(shape.Text))
                    {
                        string fw = shape.FontBold ? "bold" : "normal";
                        string fs = shape.FontItalic ? "italic" : "normal";
                        string td = shape.FontUnderline ? "text-decoration=\"underline\"" : "";
                        svg.AppendLine($"  <text x=\"{cx}\" y=\"{cy}\" font-family=\"{shape.FontName}\" font-size=\"{shape.FontSize}\" font-weight=\"{fw}\" font-style=\"{fs}\" fill=\"{fontColorHex}\" text-anchor=\"middle\" dy=\".3em\" {td} {transform}>{System.Security.SecurityElement.Escape(shape.Text)}</text>");
                    }
                }
                svg.AppendLine("</svg>");
                File.WriteAllText(filePath, svg.ToString(), Encoding.UTF8);
            });
        }
    }
}
