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

            // 註冊組件解析事件作為備用防線
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            Application.ThreadException += (sender, args) =>
            {
                string msg = $"發生未預期的錯誤: \n{args.Exception.Message}\n\n請確認 Library 資料夾內是否缺少必要的元件檔案。";
                MessageBox.Show(msg, "系統錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // 使用獨立方法啟動，防止 JIT 編譯器在載入 Library 前崩潰
            LaunchApplication();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LaunchApplication()
        {
            Application.Run(new App_UI_MainForm());
        }

        // 引導 C# 尋找 Library 資料夾中的套件
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
            return null;
        }
    }
}
