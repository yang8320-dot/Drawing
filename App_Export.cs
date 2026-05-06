/*
 * 檔案功能：處理匯出 PNG (透明) 與 PDF 功能
 * 對應選單：Export
 * 對應資料庫：無
 * 資料表名稱：無
 */
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
    }
}
