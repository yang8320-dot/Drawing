using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        
        // 定義自動存檔的路徑
        private static string AutoSavePath => Path.Combine(SaveDirectory, "autosave.draw");

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All, 
            Formatting = Formatting.None, // 優化：移除縮排，因為已使用 GZIP 壓縮，減少記憶體使用
            NullValueHandling = NullValueHandling.Ignore 
        };

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory)) 
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        // 優化：寫入 GZIP 壓縮檔案，大幅縮減檔案大小 (尤其是圖片 Base64)
        private static void WriteCompressedFile(string filePath, string content)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            using (GZipStream compressionStream = new GZipStream(fileStream, CompressionMode.Compress))
            using (StreamWriter writer = new StreamWriter(compressionStream))
            {
                writer.Write(content);
            }
        }

        // 優化：讀取並解壓 GZIP 檔案 (支援舊版無壓縮檔案自動回退機制)
        private static string ReadCompressedFile(string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                using (GZipStream decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(decompressionStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (InvalidDataException)
            {
                // 如果拋出 InvalidDataException，表示這不是一個 GZIP 壓縮檔，而是舊版的明文 JSON
                return File.ReadAllText(filePath);
            }
        }

        public static bool SaveProject(DrawProject project)
        {
            try
            {
                EnsureDirectory(); 
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string json = JsonConvert.SerializeObject(project, jsonSettings);
                        WriteCompressedFile(sfd.FileName, json);
                        
                        MessageBox.Show("專案存檔成功！", "系統通知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        DeleteAutoSave();
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
                EnsureDirectory(); 
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        string json = ReadCompressedFile(ofd.FileName);
                        
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

        public static void PerformAutoSave(DrawProject project)
        {
            try
            {
                EnsureDirectory();
                string json = JsonConvert.SerializeObject(project, jsonSettings);
                // 優化：背景存檔同樣進行壓縮，提升磁碟 I/O 寫入效能
                WriteCompressedFile(AutoSavePath, json);
            }
            catch { /* 背景存檔出錯時不干擾使用者 */ }
        }

        public static DrawProject CheckAndLoadAutoSave()
        {
            if (File.Exists(AutoSavePath))
            {
                var result = MessageBox.Show("系統發現有未正常關閉的自動存檔，是否要恢復之前的進度？", "恢復備份", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        string json = ReadCompressedFile(AutoSavePath);
                        var project = JsonConvert.DeserializeObject<DrawProject>(json, jsonSettings);
                        if (project != null && project.Pages != null) return project;
                    }
                    catch
                    {
                        MessageBox.Show("自動存檔檔案已損毀，無法恢復。", "恢復失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                
                DeleteAutoSave();
            }
            return null;
        }

        public static void DeleteAutoSave()
        {
            try
            {
                if (File.Exists(AutoSavePath)) File.Delete(AutoSavePath);
            }
            catch { }
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
