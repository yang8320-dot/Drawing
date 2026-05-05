// ==========================================
// 程式功能：SQLite 全域資料庫管理、資料表初始化
// 對應選單：系統後台
// 對應資料庫：DrawingApp.db
// 對應資料表：App_CanvasEngine
// ==========================================
using System;
using System.Data.SQLite;
using System.IO;
using Dapper;

namespace PortableDrawingApp
{
    public static class App_DatabaseManager
    {
        // 依規範：主資料庫名稱以主選單/主系統名稱為準
        private static readonly string dbPath = "DrawingApp.db";
        public static readonly string ConnectionString = $"Data Source={dbPath};Version=3;";

        public static void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                
                // 依規範：資料表命名與對應的 .cs 檔名完全一致
                string createCanvasTable = @"
                    CREATE TABLE IF NOT EXISTS App_CanvasEngine (
                        Id TEXT PRIMARY KEY,
                        ParentId TEXT,
                        TypeStr TEXT,
                        X REAL, Y REAL, Width REAL, Height REAL,
                        BorderColorHex TEXT,
                        Content TEXT,
                        CreatedAt TEXT
                    );";
                conn.Execute(createCanvasTable);
            }
        }
    }
}
