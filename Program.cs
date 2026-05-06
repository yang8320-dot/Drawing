using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DrawingApp
{
    static class Program
    {
        // 匯入 Windows 底層 API 以強制載入 Native DLL
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        private static Mutex _mutex;

        [STAThread]
        static void Main()
        {
            _mutex = new Mutex(true, "DrawingApp_Commercial_Unique_ID", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("繪圖程式已經在執行中！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 1. 強制讓系統先去 Library 裡面尋找 C++ 底層依賴檔 (如 SQLite)
            PreLoadNativeLibraries();

            // 2. 攔截一般 C# 程式庫 (如 Newtonsoft.Json) 去 Library 尋找
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            Application.ThreadException += (sender, args) =>
            {
                string msg = $"發生未預期的錯誤: \n{args.Exception.Message}\n\n請確認 Library 資料夾內是否缺少必要的元件檔案。";
                MessageBox.Show(msg, "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            LaunchApplication();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        // 強制系統去 Library 資料夾載入 x64 或 x86 的 Native DLL
        private static void PreLoadNativeLibraries()
        {
            try
            {
                string arch = Environment.Is64BitProcess ? "x64" : "x86";
                string interopPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library", arch, "SQLite.Interop.dll");
                
                if (File.Exists(interopPath))
                {
                    LoadLibrary(interopPath);
                }
            }
            catch { /* 忽略底層載入錯誤，交由後續邏輯處理 */ }
        }

        // 引導 C# 核心套件至 Library 資料夾
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
