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
        // 【關鍵修正1】：靜態建構子 (Static Constructor) 
        // 這是 C# 中最早執行的區塊，確保在 JIT 觸碰任何 UI 之前，就教會系統去 Library 找檔案
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        [STAThread]
        static void Main()
        {
            // 【關鍵修正2】：全域 Try-Catch 防護，只要有錯絕對會寫入日誌檔並彈出視窗
            try
            {
                Mutex mutex = new Mutex(true, "DrawingApp_Commercial_Unique_ID", out bool createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("繪圖程式已經在執行中！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // 攔截執行期間的錯誤
                Application.ThreadException += (sender, args) =>
                {
                    MessageBox.Show($"執行時期錯誤: \n{args.Exception.Message}", "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                // 進入主視窗
                LaunchApplication();
            }
            catch (Exception ex)
            {
                // 如果發生嚴重閃退，強制寫出日誌檔
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                File.WriteAllText(logPath, "發生致命錯誤時間: " + DateTime.Now + Environment.NewLine + ex.ToString());
                MessageBox.Show($"發生嚴重啟動錯誤！\n請查看程式目錄下的 crash_log.txt 取得詳細資訊。\n\n錯誤訊息: {ex.Message}", "致命錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 獨立的方法，防止 JIT 過早解析 App_UI_MainForm
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        // 引導 C# 去 Library 資料夾尋找 DLL
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");
                string assemblyName = new AssemblyName(args.Name).Name;
                string assemblyPath = Path.Combine(folderPath, assemblyName + ".dll");
                
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }
            catch { }
            return null; // 找不到就回傳 null 交給系統處理
        }
    }
}
