// ==========================================
// 檔案功能：程式啟動進入點、全域異常處理
// 對應選單：系統啟動
// 對應資料庫：無
// 對應資料表：無
// ==========================================
using System;
using System.Windows.Forms;

namespace PortableDrawingApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 實作全域 Thread-Safety 異常捕捉，防止程式崩潰
            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"UI Thread Error: {args.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Background Thread Error: {((Exception)args.ExceptionObject).Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}
