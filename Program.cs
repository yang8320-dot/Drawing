using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace DrawingApp
{
    static class Program
    {
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        [STAThread]
        static void Main()
        {
            try
            {
                // 【Req 7: 驗證使用者權限】
                if (!LicenseManager.VerifyLicense())
                {
                    MessageBox.Show("驗證失敗，非授權使用者！\n\n您沒有權限執行此程式，系統即將關閉。", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Mutex mutex = new Mutex(true, "DrawingApp_Commercial_Unique_ID", out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("繪圖程式已經在執行中！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                Application.ThreadException += (sender, args) =>
                {
                    MessageBox.Show($"執行時期錯誤: \n{args.Exception.Message}", "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                LaunchApplication();
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                File.WriteAllText(logPath, "發生致命錯誤時間: " + DateTime.Now + Environment.NewLine + ex.ToString());
                MessageBox.Show($"發生嚴重啟動錯誤！\n請查看程式目錄下的 crash_log.txt 取得詳細資訊。\n\n錯誤訊息: {ex.Message}", "致命錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");
                string assemblyName = new AssemblyName(args.Name).Name;
                string assemblyPath = Path.Combine(folderPath, assemblyName + ".dll");
                
                if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);
            }
            catch { }
            return null;
        }
    }
}
