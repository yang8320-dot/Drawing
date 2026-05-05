// ==========================================
// 程式功能：程式啟動進入點、全域執行緒安全與異常處理
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
            
            // 實作全域執行緒安全 (Thread-Safety)，防止背景資料庫讀寫導致 UI 崩潰
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
