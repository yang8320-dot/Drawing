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
            // --- 4. 防止程式重複開啟 ---
            _mutex = new Mutex(true, "DrawingApp_Commercial_Unique_ID", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("繪圖程式已經在執行中！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // --- 3. 徹底解決 Library 資料夾打包與 JSON 找不到的問題 ---
            // 必須在觸碰任何 UI 或 Json 程式碼之前，先註冊事件
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            // 啟用視覺樣式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 攔截全域例外
            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"發生未預期的錯誤: \n{args.Exception.Message}", "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // 使用獨立的方法來啟動主程式，這是為了防止 JIT 編譯器提早尋找 DLL
            LaunchApplication();
        }

        // 告訴編譯器不要把這個方法跟 Main 合併，確保 AssemblyResolve 優先執行
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            // 系統找不到 DLL 時，強制導向子資料夾 "Library"
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");
            string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
            
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            return null;
        }
    }
}
