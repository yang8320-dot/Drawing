/*
 * 檔案功能：應用程式進入點與全域例外處理
 * 對應選單：系統啟動
 * 對應資料庫：無
 * 資料表名稱：無
 */
using System;
using System.Windows.Forms;

namespace DrawingApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 確保 UI 執行緒安全，攔截未處理的例外
            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"Application Error: {args.Exception.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new App_UI_MainForm());
        }
    }
}
