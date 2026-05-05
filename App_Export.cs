using System;
using System.Drawing;
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
        // 匯出透明 PNG
        public static async Task ExportToPngAsync(Bitmap canvasBitmap, string filePath)
        {
            await Task.Run(() =>
            {
                canvasBitmap.Save(filePath, ImageFormat.Png);
            });
        }

        // 匯出 PDF (A4 尺寸縮放)
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
                    
                    double ratio = Math.Min(page.Width / image.PixelWidth, page.Height / image.PixelHeight);
                    gfx.DrawImage(image, 0, 0, image.PixelWidth * ratio, image.PixelHeight * ratio);
                }
                document.Save(filePath);
            });
        }

        // --- 進階功能：匯出 SVG 向量圖 ---
        public static async Task ExportToSvgAsync(System.Collections.Generic.List<App_Shapes.ShapeBase> shapes, SizeF pageSize, string filePath)
        {
            await Task.Run(() =>
            {
                StringBuilder svg = new StringBuilder();
                svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                svg.AppendLine($"<svg width=\"{pageSize.Width}\" height=\"{pageSize.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

                // 建立背景
                svg.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"white\" />");

                foreach (var shape in shapes)
                {
                    string colorStr = $"rgb({shape.ShapeColor.R},{shape.ShapeColor.G},{shape.ShapeColor.B})";
                    string fontColorStr = $"rgb({shape.FontColor.R},{shape.FontColor.G},{shape.FontColor.B})";
                    string dashArray = shape.StrokeDashStyle == System.Drawing.Drawing2D.DashStyle.Dash ? "stroke-dasharray=\"5,5\"" :
                                       shape.StrokeDashStyle == System.Drawing.Drawing2D.DashStyle.Dot ? "stroke-dasharray=\"2,2\"" : "";

                    // SVG 節點生成
                    if (shape is App_Shapes.ConnectorShape conn)
                    {
                        // 簡單直線對應 SVG path (暫不處理複雜的 A* 轉折點輸出)
                        svg.AppendLine($"<path d=\"M {conn.StartPt.X} {conn.StartPt.Y} L {conn.EndPt.X} {conn.EndPt.Y}\" stroke=\"{colorStr}\" stroke-width=\"{shape.StrokeWidth}\" fill=\"none\" {dashArray} />");
                    }
                    else if (shape is App_Shapes.RectShape || shape is App_Shapes.TextNodeShape)
                    {
                        if (!(shape is App_Shapes.TextNodeShape tn && tn.IsTransparent))
                        {
                            svg.AppendLine($"<rect x=\"{shape.Bounds.X}\" y=\"{shape.Bounds.Y}\" width=\"{shape.Bounds.Width}\" height=\"{shape.Bounds.Height}\" stroke=\"{colorStr}\" stroke-width=\"{shape.StrokeWidth}\" fill=\"none\" {dashArray} />");
                        }
                    }
                    else if (shape is App_Shapes.CircleShape)
                    {
                        float cx = shape.Bounds.X + shape.Bounds.Width / 2;
                        float cy = shape.Bounds.Y + shape.Bounds.Height / 2;
                        float rx = shape.Bounds.Width / 2;
                        float ry = shape.Bounds.Height / 2;
                        svg.AppendLine($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{rx}\" ry=\"{ry}\" stroke=\"{colorStr}\" stroke-width=\"{shape.StrokeWidth}\" fill=\"none\" {dashArray} />");
                    }
                    else if (shape is App_Shapes.ImageShape img)
                    {
                        svg.AppendLine($"<image x=\"{shape.Bounds.X}\" y=\"{shape.Bounds.Y}\" width=\"{shape.Bounds.Width}\" height=\"{shape.Bounds.Height}\" href=\"data:image/png;base64,{img.Base64Image}\" />");
                    }

                    // 文字繪製 (統一在圖形中央)
                    if (!string.IsNullOrEmpty(shape.Text))
                    {
                        float textX = shape.Bounds.X + shape.Bounds.Width / 2;
                        float textY = shape.Bounds.Y + shape.Bounds.Height / 2 + shape.FontSize / 2; // 簡單對齊
                        svg.AppendLine($"<text x=\"{textX}\" y=\"{textY}\" font-family=\"{shape.FontName}\" font-size=\"{shape.FontSize}px\" fill=\"{fontColorStr}\" text-anchor=\"middle\">{shape.Text}</text>");
                    }
                }

                svg.AppendLine("</svg>");
                File.WriteAllText(filePath, svg.ToString(), Encoding.UTF8);
            });
        }
    }
}
