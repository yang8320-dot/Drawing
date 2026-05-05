/*
 * 檔案功能：SQLite 多重資料庫與資料表管理、強制日期格式轉換
 * 對應選單：全局資料存取
 * 對應資料庫：動態生成 (依選單名稱)
 * 資料表名稱：動態生成 (依 CS 檔名)
 */
using System;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace DrawingApp
{
    public static class App_Database
    {
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";

        // 非同步初始化資料庫與資料表
        public static async Task InitializeDatabaseAsync(string menuName, string csFileName)
        {
            string dbName = $"{menuName}.sqlite";
            string connectionString = $"Data Source={dbName};Version=3;";

            await Task.Run(() =>
            {
                if (!File.Exists(dbName))
                {
                    SQLiteConnection.CreateFile(dbName);
                }

                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string createTableQuery = $@"
                        CREATE TABLE IF NOT EXISTS {csFileName} (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ShapeData TEXT NOT NULL,
                            CreatedAt TEXT NOT NULL
                        )";
                    using (var cmd = new SQLiteCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            });
        }

        // 取得標準化時間字串
        public static string GetFormattedDate(DateTime date)
        {
            return date.ToString(DATE_FORMAT);
        }

        // 非同步寫入繪圖資料 (將形狀序列化後存入)
        public static async Task SaveDrawingDataAsync(string menuName, string csFileName, string jsonData)
        {
            string dbName = $"{menuName}.sqlite";
            string connectionString = $"Data Source={dbName};Version=3;";

            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string insertQuery = $"INSERT INTO {csFileName} (ShapeData, CreatedAt) VALUES (@data, @date)";
                    using (var cmd = new SQLiteCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@data", jsonData);
                        cmd.Parameters.AddWithValue("@date", GetFormattedDate(DateTime.Now));
                        cmd.ExecuteNonQuery();
                    }
                }
            });
        }
    }
}
