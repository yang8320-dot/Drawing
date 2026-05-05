// ==========================================
// 檔案功能：背景檔案異動監控與日誌非同步寫入
// 對應選單：File Watcher
// 對應資料庫：NotificationCenter.db
// 對應資料表：App_FileWatcher
// ==========================================
using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapper;

namespace NotificationCenter
{
    public class App_FileWatcher : UserControl
    {
        private ListBox lstLogs;
        private FileSystemWatcher watcher;

        public App_FileWatcher()
        {
            InitializeComponent();
            App_DatabaseManager.InitializeDatabase();
            InitializeWatcher();
            _ = LoadLogsAsync();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;

            Label lblTitle = new Label 
            { 
                Text = "C:\\ System Drive Activity Logs:", 
                Dock = DockStyle.Top, 
                Height = 30, 
                Font = new Font("Arial", 10, FontStyle.Bold),
                Padding = new Padding(10, 5, 0, 0)
            };
            this.Controls.Add(lblTitle);

            lstLogs = new ListBox 
            { 
                Dock = DockStyle.Fill, 
                Font = new Font("Arial", 10), 
                Margin = new Padding(10) 
            };
            this.Controls.Add(lstLogs);
            lstLogs.BringToFront();
        }

        private void InitializeWatcher()
        {
            // 監聽背景系統驅動器變更，避免 UI 卡頓
            watcher = new FileSystemWatcher("C:\\", "*.txt")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            watcher.Created += Watcher_Event;
            watcher.Changed += Watcher_Event;
        }

        private async void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            string msg = $"File {e.ChangeType}: {e.Name}";
            string createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(App_DatabaseManager.ConnectionString))
                {
                    string sql = "INSERT INTO App_FileWatcher (Id, LogMessage, CreatedAt) VALUES (@Id, @LogMessage, @CreatedAt)";
                    conn.Execute(sql, new { Id = Guid.NewGuid().ToString(), LogMessage = msg, CreatedAt = createdAt });
                }
            });

            // 使用 Invoke 確保安全更新 UI
            if (this.IsHandleCreated)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    lstLogs.Items.Insert(0, $"[{createdAt}] {msg}");
                });
            }
        }

        private async Task LoadLogsAsync()
        {
            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(App_DatabaseManager.ConnectionString))
                {
                    var logs = conn.Query("SELECT LogMessage, CreatedAt FROM App_FileWatcher ORDER BY CreatedAt DESC LIMIT 50");
                    
                    this.Invoke((MethodInvoker)delegate
                    {
                        lstLogs.Items.Clear();
                        foreach (var log in logs)
                        {
                            lstLogs.Items.Add($"[{log.CreatedAt}] {log.LogMessage}");
                        }
                    });
                }
            });
        }
    }
}
