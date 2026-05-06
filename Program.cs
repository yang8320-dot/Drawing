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
        // 定義全域 Mutex 防止程式重複開啟
        private static Mutex _mutex;

        [STAThread]
        static void Main()
        {
            // 防止程式重複開啟
            _mutex = new Mutex(true, "DrawingApp_Commercial_Unique_ID", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("繪圖程式已經在執行中！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 【關鍵修正】在觸碰任何 UI 或 Json 程式碼之前，先註冊事件
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            // 啟用視覺樣式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 攔截全域例外，提供更友善的錯誤訊息
            Application.ThreadException += (sender, args) =>
            {
                string msg = $"發生未預期的錯誤: \n{args.Exception.Message}\n\n請確認 Library 資料夾內是否缺少必要的 DLL 檔案。";
                MessageBox.Show(msg, "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // 使用獨立的方法來啟動主程式，防止 JIT 編譯器提早尋找 DLL
            LaunchApplication();
        }

        // 告訴編譯器不要把這個方法跟 Main 合併，確保 AssemblyResolve 優先執行
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        // 解析並載入外部 DLL
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                // 系統找不到 DLL 時，強制導向子資料夾 "Library"
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");
                string assemblyName = new AssemblyName(args.Name).Name;
                string assemblyPath = Path.Combine(folderPath, assemblyName + ".dll");
                
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }
            catch 
            { 
                // 忽略解析過程中可能發生的名稱解析錯誤
            }
            
            return null;
        }
    }
}
