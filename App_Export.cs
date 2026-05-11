// ============================================================
// FILE: App_Export.cs
// ============================================================

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

                int width = (int)(maxX - minX + 40); 
                int height = (int)(maxY - minY + 40);

                if (width <= 0 || height <= 0) return;

                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(-minX + 20, -minY + 20);

                        var selectionStates = selectedShapes.ToDictionary(s => s, s => s.IsSelected);
                        foreach (var s in selectedShapes) s.IsSelected = false;

                        foreach (var s in selectedShapes) s.DrawWithTransform(g);

                        foreach (var s in selectedShapes) s.IsSelected = selectionStates[s];
                    }
                    bmp.Save(filePath, ImageFormat.Png);
                }
            });
        }

        public static async Task ExportToPdfMultiPageAsync(App_CanvasControl canvas, string filePath)
        {
            await Task.Run(() =>
            {
                PdfDocument document = new PdfDocument();
                
                SizeF actualSize = canvas.ActualPageSize;
                SizeF singlePageSize = canvas.PageSize;

                int cols = (int)Math.Ceiling(actualSize.Width / singlePageSize.Width);
                int rows = (int)Math.Ceiling(actualSize.Height / singlePageSize.Height);

                using (Bitmap fullBitmap = canvas.GetTransparentCanvasRender())
                {
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            PdfPage page = document.AddPage();
                            page.Width = singlePageSize.Width / 3.5f; 
                            page.Height = singlePageSize.Height / 3.5f;

                            Rectangle cropRect = new Rectangle(
                                (int)(c * singlePageSize.Width), 
                                (int)(r * singlePageSize.Height), 
                                (int)singlePageSize.Width, 
                                (int)singlePageSize.Height);

                            if (cropRect.Right > fullBitmap.Width) cropRect.Width = fullBitmap.Width - cropRect.X;
                            if (cropRect.Bottom > fullBitmap.Height) cropRect.Height = fullBitmap.Height - cropRect.Y;

                            if (cropRect.Width <= 0 || cropRect.Height <= 0) continue;

                            using (Bitmap pageBitmap = fullBitmap.Clone(cropRect, fullBitmap.PixelFormat))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                pageBitmap.Save(ms, ImageFormat.Png);
                                ms.Position = 0;
                                XImage image = XImage.FromStream(ms);

                                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                                {
                                    double drawW = image.PixelWidth / 3.5f;
                                    double drawH = image.PixelHeight / 3.5f;
                                    gfx.DrawImage(image, 0, 0, drawW, drawH);
                                    
                                    if (canvas.ShowPageNumbers)
                                    {
                                        int pageNum = r * cols + c + 1;
                                        int totalPages = cols * rows;
                                        string pageText = $"{canvas.CanvasTitle} - 第 {pageNum} 頁 / 共 {totalPages} 頁";
                                        
                                        // 【修正】：將文字畫成圖片再貼到 PDF，避開 PDFsharp 字型錯誤
                                        using (Font gdiFont = new Font("微軟正黑體", 16, FontStyle.Regular))
                                        {
                                            SizeF textSize;
                                            using (Bitmap tempBmp = new Bitmap(1, 1))
                                            using (Graphics tempG = Graphics.FromImage(tempBmp))
                                            {
                                                textSize = tempG.MeasureString(pageText, gdiFont);
                                            }

                                            using (Bitmap textBmp = new Bitmap((int)Math.Ceiling(textSize.Width), (int)Math.Ceiling(textSize.Height)))
                                            {
                                                using (Graphics gText = Graphics.FromImage(textBmp))
                                                {
                                                    gText.SmoothingMode = SmoothingMode.AntiAlias;
                                                    gText.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                                                    gText.Clear(Color.Transparent);
                                                    gText.DrawString(pageText, gdiFont, Brushes.Gray, 0, 0);
                                                }

                                                using (MemoryStream msText = new MemoryStream())
                                                {
                                                    textBmp.Save(msText, ImageFormat.Png);
                                                    msText.Position = 0;
                                                    XImage textImage = XImage.FromStream(msText);
                                                    
                                                    // 在 PDF 底部置中繪製轉成圖片的頁碼
                                                    double textDrawW = textImage.PixelWidth / 3.5f;
                                                    double textDrawH = textImage.PixelHeight / 3.5f;
                                                    double xCenter = (page.Width - textDrawW) / 2;
                                                    gfx.DrawImage(textImage, xCenter, page.Height - textDrawH - 10, textDrawW, textDrawH);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                document.Save(filePath);
            });
        }

        private static string ToSvgColor(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static async Task ExportToSvgAsync(List<App_Shapes.ShapeBase> shapes, SizeF pageSize, string filePath)
        {
            await Task.Run(() =>
            {
                StringBuilder svg = new StringBuilder();
                svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                svg.AppendLine($"<svg width=\"{pageSize.Width}\" height=\"{pageSize.Height}\" viewBox=\"0 0 {pageSize.Width} {pageSize.Height}\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");

                foreach (var shape in shapes)
                {
                    string strokeHex = ToSvgColor(shape.ShapeColor);
                    string fillHex = shape.FillColor == Color.Transparent ? "none" : ToSvgColor(shape.FillColor);
                    string fontColorHex = ToSvgColor(shape.FontColor);
                    string dashArray = shape.StrokeDashStyle == System.Drawing.Drawing2D.DashStyle.Dash ? "stroke-dasharray=\"5,5\"" : "";
                    
                    float x = shape.Bounds.X;
                    float y = shape.Bounds.Y;
                    float w = shape.Bounds.Width;
                    float h = shape.Bounds.Height;
                    float cx = x + w / 2;
                    float cy = y + h / 2;
                    
                    string transform = shape.RotationAngle != 0 ? $"transform=\"rotate({shape.RotationAngle},{cx},{cy})\"" : "";

                    if (shape is App_Shapes.RectShape)
                        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
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
                        svg.AppendLine($"  <ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{w/2}\" ry=\"{h/2}\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    else if (shape is App_Shapes.ConnectorShape conn)
                        svg.AppendLine($"  <line x1=\"{conn.StartPt.X}\" y1=\"{conn.StartPt.Y}\" x2=\"{conn.EndPt.X}\" y2=\"{conn.EndPt.Y}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
                    else if (shape is App_Shapes.FreehandShape fh)
                    {
                        if (fh.LocalPoints.Count > 1)
                        {
                            svg.Append($"  <polyline points=\"");
                            foreach (var pt in fh.LocalPoints) svg.Append($"{fh.Bounds.X + pt.X},{fh.Bounds.Y + pt.Y} ");
                            svg.AppendLine($"\" fill=\"none\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" {dashArray} {transform} />");
                        }
                    }
                    else if (shape is App_Shapes.BezierShape bezier)
                    {
                        if (bezier.LocalNodes.Count > 1)
                        {
                            svg.Append($"  <path d=\"M {bezier.Bounds.X + bezier.LocalNodes[0].Anchor.X} {bezier.Bounds.Y + bezier.LocalNodes[0].Anchor.Y} ");
                            for (int i = 1; i < bezier.LocalNodes.Count; i++)
                                svg.Append($"C {bezier.Bounds.X + bezier.LocalNodes[i - 1].Control2.X} {bezier.Bounds.Y + bezier.LocalNodes[i - 1].Control2.Y}, {bezier.Bounds.X + bezier.LocalNodes[i].Control1.X} {bezier.Bounds.Y + bezier.LocalNodes[i].Control1.Y}, {bezier.Bounds.X + bezier.LocalNodes[i].Anchor.X} {bezier.Bounds.Y + bezier.LocalNodes[i].Anchor.Y} ");
                            svg.AppendLine($"\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" {dashArray} {transform} />");
                        }
                    }
                    else if (shape is App_Shapes.DoubleArrowShape || shape is App_Shapes.BlockArrowShape || shape is App_Shapes.BraceLeftShape || shape is App_Shapes.BraceRightShape || shape is App_Shapes.Branch1To2Shape || shape is App_Shapes.Branch1To3Shape || shape is App_Shapes.Branch1To4Shape || shape is App_Shapes.TriangleShape || shape is App_Shapes.DiamondShape || shape is App_Shapes.StarShape || shape is App_Shapes.PentagonShape || shape is App_Shapes.HexagonShape)
                    {
                        PointF[] pts = null;
                        if (shape is App_Shapes.TriangleShape ts) pts = ts.GetPolygonPoints();
                        else if (shape is App_Shapes.DiamondShape ds) pts = ds.GetPolygonPoints();
                        else if (shape is App_Shapes.StarShape ss) pts = ss.GetPolygonPoints();
                        else if (shape is App_Shapes.PentagonShape ps) pts = ps.GetPolygonPoints();
                        else if (shape is App_Shapes.HexagonShape hs) pts = hs.GetPolygonPoints();
                        else if (shape is App_Shapes.DoubleArrowShape das) pts = das.GetPolygonPoints();
                        
                        if (pts != null)
                        {
                            svg.Append($"  <polygon points=\"");
                            foreach (var pt in pts) svg.Append($"{pt.X},{pt.Y} ");
                            svg.AppendLine($"\" fill=\"{fillHex}\" stroke=\"{strokeHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                        }
                    }

                    if (!string.IsNullOrEmpty(shape.Text))
                    {
                        string fw = shape.FontBold ? "bold" : "normal";
                        string fs = shape.FontItalic ? "italic" : "normal";
                        string td = shape.FontUnderline ? "text-decoration=\"underline\"" : "";
                        svg.AppendLine($"  <text x=\"{cx}\" y=\"{cy}\" font-family=\"{shape.FontName}\" font-size=\"{shape.FontSize}\" font-weight=\"{fw}\" font-style=\"{fs}\" fill=\"{fontColorHex}\" text-anchor=\"middle\" dominant-baseline=\"central\" {td} {transform}>{System.Security.SecurityElement.Escape(shape.Text)}</text>");
                    }
                }
                svg.AppendLine("</svg>");
                File.WriteAllText(filePath, svg.ToString(), Encoding.UTF8);
            });
        }
    }
}
