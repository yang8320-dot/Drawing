/*
 * 檔案功能：SQLite 多重資料庫與資料表管理、強制日期格式轉換
 * 對應選單：全局資料存取
 * 對應資料庫：動態生成 (依選單名稱)
 * 資料表名稱：動態生成 (依 CS 檔名)
 */
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace DrawingApp
{
    public static class App_Database
    {
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
        private static string SaveDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save");

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
        }

        // 非同步初始化資料庫與資料表
        public static async Task InitializeDatabaseAsync(string menuName, string csFileName)
        {
            EnsureDirectory();
            string dbName = Path.Combine(SaveDirectory, $"{menuName}.sqlite");
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

        public static string GetFormattedDate(DateTime date)
        {
            return date.ToString(DATE_FORMAT);
        }

        // 非同步寫入繪圖資料 (將形狀序列化後存入)
        public static async Task SaveDrawingDataAsync(string menuName, string csFileName, string jsonData)
        {
            await InitializeDatabaseAsync(menuName, csFileName);
            
            string dbName = Path.Combine(SaveDirectory, $"{menuName}.sqlite");
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

        // 新增：從資料庫讀取最新的一筆紀錄
        public static async Task<string> LoadLatestDrawingDataAsync(string menuName, string csFileName)
        {
            await InitializeDatabaseAsync(menuName, csFileName);

            string dbName = Path.Combine(SaveDirectory, $"{menuName}.sqlite");
            string connectionString = $"Data Source={dbName};Version=3;";
            string result = null;

            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string selectQuery = $"SELECT ShapeData FROM {csFileName} ORDER BY Id DESC LIMIT 1";
                    using (var cmd = new SQLiteCommand(selectQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = reader["ShapeData"].ToString();
                        }
                    }
                }
            });
            return result;
        }
    }
}
