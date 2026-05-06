using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DrawingApp
{
    public class DrawProject
    {
        public List<DrawPage> Pages { get; set; } = new List<DrawPage>();
    }

    public class DrawPage
    {
        public string Title { get; set; }
        public List<App_Shapes.ShapeBase> Shapes { get; set; } = new List<App_Shapes.ShapeBase>();
    }

    public static class App_SaveLoad
    {
        private static string SaveDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save");

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All, 
            Formatting = Formatting.Indented
        };

        // 自動建立 save 資料夾
        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory)) 
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        // --- 優化：變更回傳型態為 bool，讓外部可以判斷是否存檔成功以解除 Dirty Flag ---
        public static bool SaveProject(DrawProject project)
        {
            try
            {
                EnsureDirectory(); // 動作前先確保資料夾存在
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string json = JsonConvert.SerializeObject(project, jsonSettings);
                        File.WriteAllText(sfd.FileName, json);
                        MessageBox.Show("專案存檔成功！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"存檔時發生錯誤: {ex.Message}", "存檔失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        public static DrawProject LoadProject()
        {
            try
            {
                EnsureDirectory(); // 動作前先確保資料夾存在
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string json = File.ReadAllText(ofd.FileName);
                        try 
                        {
                            var project = JsonConvert.DeserializeObject<DrawProject>(json, jsonSettings);
                            if (project != null && project.Pages != null) return project;
                        }
                        catch { }

                        try
                        {
                            var oldShapes = JsonConvert.DeserializeObject<List<App_Shapes.ShapeBase>>(json, jsonSettings);
                            return new DrawProject { Pages = new List<DrawPage> { new DrawPage { Title = "舊版畫布", Shapes = oldShapes } } };
                        }
                        catch 
                        { 
                            MessageBox.Show("檔案格式錯誤或已損毀！", "讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"讀取檔案時發生錯誤: {ex.Message}", "讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }

        public static List<App_Shapes.ShapeBase> CloneShapes(List<App_Shapes.ShapeBase> shapes)
        {
            try
            {
                if (shapes == null || shapes.Count == 0) return new List<App_Shapes.ShapeBase>();
                string json = JsonConvert.SerializeObject(shapes, jsonSettings);
                return JsonConvert.DeserializeObject<List<App_Shapes.ShapeBase>>(json, jsonSettings) ?? new List<App_Shapes.ShapeBase>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"複製圖形失敗: {ex.Message}\n\n請確認元件是否正確載入。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<App_Shapes.ShapeBase>();
            }
        }
    }
}
