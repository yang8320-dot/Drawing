/*
 * 檔案功能：處理匯出 PNG (透明)、PDF 與 SVG 功能
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
                    // --- 新增：圓角矩形 SVG ---
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
                    // --- 修正：移除 dynamic，改用模式比對來獲取多邊形的節點 ---
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
                    // --- 新增：雲朵 SVG (多個橢圓組合) ---
                    else if (shape is App_Shapes.CloudShape)
                    {
                        svg.AppendLine($"  <g {transform}>");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.35f}\" cy=\"{y + h * 0.45f}\" rx=\"{w * 0.2f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.60f}\" cy=\"{y + h * 0.40f}\" rx=\"{w * 0.25f}\" ry=\"{h * 0.30f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.75f}\" cy=\"{y + h * 0.60f}\" rx=\"{w * 0.17f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.50f}\" cy=\"{y + h * 0.65f}\" rx=\"{w * 0.25f}\" ry=\"{h * 0.25f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        svg.AppendLine($"    <ellipse cx=\"{x + w * 0.25f}\" cy=\"{y + h * 0.60f}\" rx=\"{w * 0.15f}\" ry=\"{h * 0.20f}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                        
                        // 用一個沒有邊框的橢圓填補中間的線條空隙
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
                        svg.AppendLine($"  <text x=\"{cx}\" y=\"{cy}\" font-family=\"{shape.FontName}\" font-size=\"{shape.FontSize}\" font-weight=\"{fw}\" font-style=\"{fs}\" fill=\"{fontColorHex}\" text-anchor=\"middle\" dominant-baseline=\"middle\" {td} {transform}>{System.Security.SecurityElement.Escape(shape.Text)}</text>");
                    }
                }
                svg.AppendLine("</svg>");
                File.WriteAllText(filePath, svg.ToString(), Encoding.UTF8);
            });
        }
    }
}
