using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DrawingApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 強制系統在執行時，如果找不到 DLL，就去 "Library" 資料夾裡面找
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");
                string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);
                return null;
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 確保 UI 執行緒安全，攔截未處理的例外
            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"Application Error: {args.Exception.Message}", "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new App_UI_MainForm());
        }
    }
}
