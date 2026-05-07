/*
 * 檔案功能：處理匯出 PNG (透明)、PDF 與新增的 SVG 功能
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
        // 匯出透明 PNG
        public static async Task ExportToPngAsync(Bitmap canvasBitmap, string filePath)
        {
            await Task.Run(() =>
            {
                canvasBitmap.Save(filePath, ImageFormat.Png);
            });
        }

        // 匯出 PDF (A4 尺寸)
        public static async Task ExportToPdfAsync(Bitmap canvasBitmap, string filePath, bool isLandscape)
        {
            await Task.Run(() =>
            {
                PdfDocument document = new PdfDocument();
                PdfPage page = document.AddPage();
                
                // 設定 A4 尺寸
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = isLandscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                using (MemoryStream ms = new MemoryStream())
                {
                    canvasBitmap.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    XImage image = XImage.FromStream(ms);
                    
                    // 根據 A4 尺寸繪製圖片 (這裡採等比例縮放)
                    gfx.DrawImage(image, 0, 0, page.Width, page.Height);
                }
                document.Save(filePath);
            });
        }

        // --- 新增：匯出 SVG 向量圖 ---
        public static async Task ExportToSvgAsync(List<App_Shapes.ShapeBase> shapes, SizeF pageSize, string filePath)
        {
            await Task.Run(() =>
            {
                StringBuilder svg = new StringBuilder();
                svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                svg.AppendLine($"<svg width=\"{pageSize.Width}\" height=\"{pageSize.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

                foreach (var shape in shapes)
                {
                    string colorHex = ColorTranslator.ToHtml(shape.ShapeColor);
                    string fontColorHex = ColorTranslator.ToHtml(shape.FontColor);
                    string dashArray = shape.StrokeDashStyle == System.Drawing.Drawing2D.DashStyle.Dash ? "stroke-dasharray=\"5,5\"" : "";
                    
                    float x = shape.Bounds.X;
                    float y = shape.Bounds.Y;
                    float w = shape.Bounds.Width;
                    float h = shape.Bounds.Height;
                    float cx = x + w / 2;
                    float cy = y + h / 2;
                    
                    string transform = shape.RotationAngle != 0 ? $"transform=\"rotate({shape.RotationAngle},{cx},{cy})\"" : "";

                    if (shape is App_Shapes.RectShape || shape is App_Shapes.TextNodeShape)
                    {
                        string fill = (shape is App_Shapes.TextNodeShape tns && tns.IsTransparent) ? "none" : "none";
                        svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" fill=\"{fill}\" stroke=\"{colorHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.CircleShape)
                    {
                        svg.AppendLine($"  <ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{w/2}\" ry=\"{h/2}\" fill=\"none\" stroke=\"{colorHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} {transform} />");
                    }
                    else if (shape is App_Shapes.ConnectorShape conn)
                    {
                        svg.AppendLine($"  <line x1=\"{conn.StartPt.X}\" y1=\"{conn.StartPt.Y}\" x2=\"{conn.EndPt.X}\" y2=\"{conn.EndPt.Y}\" stroke=\"{colorHex}\" stroke-width=\"{shape.StrokeWidth}\" {dashArray} />");
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
                            svg.AppendLine($"\" fill=\"none\" stroke=\"{colorHex}\" stroke-width=\"{shape.StrokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" {dashArray} {transform} />");
                        }
                    }

                    // 處理文字
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
