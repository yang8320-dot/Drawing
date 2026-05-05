// ==========================================
// 檔案功能：SQLite 資料庫讀寫、非同步操作 (Thread-Safety)
// 對應選單：系統後台
// 對應資料庫：DrawingApp.db
// 對應資料表：App_CanvasEngine
// ==========================================
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace PortableDrawingApp
{
    public class DbManager
    {
        private readonly string dbPath = "DrawingApp.db";
        private readonly string connString;

        public DbManager()
        {
            connString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }
            using (var conn = new SQLiteConnection(connString))
            {
                conn.Open();
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS App_CanvasEngine (
                        Id TEXT PRIMARY KEY,
                        ParentId TEXT,
                        TypeStr TEXT,
                        X REAL, Y REAL, Width REAL, Height REAL,
                        BorderColorHex TEXT,
                        Content TEXT,
                        CreatedAt TEXT
                    );";
                conn.Execute(createTableSql);
            }
        }

        public async Task SaveShapesAsync(List<ShapeModel> shapes)
        {
            // 使用 Task.Run 確保不阻擋 UI 執行緒
            await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(connString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        conn.Execute("DELETE FROM App_CanvasEngine", transaction: transaction);
                        string insertSql = @"
                            INSERT INTO App_CanvasEngine (Id, ParentId, TypeStr, X, Y, Width, Height, BorderColorHex, Content, CreatedAt) 
                            VALUES (@Id, @ParentId, @TypeStr, @X, @Y, @Width, @Height, @BorderColorHex, @Content, @CreatedAt)";
                        conn.Execute(insertSql, shapes, transaction: transaction);
                        transaction.Commit();
                    }
                }
            });
        }

        public async Task<List<ShapeModel>> LoadShapesAsync()
        {
            return await Task.Run(() =>
            {
                using (var conn = new SQLiteConnection(connString))
                {
                    conn.Open();
                    return conn.Query<ShapeModel>("SELECT * FROM App_CanvasEngine ORDER BY CreatedAt ASC").ToList();
                }
            });
        }
    }
}
