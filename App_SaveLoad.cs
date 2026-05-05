using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_SaveLoad
    {
        private static string SaveDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save");

        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All, // 確保複製和存檔多型正常
            Formatting = Formatting.Indented
        };

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
        }

        public static void SaveAs(List<App_Shapes.ShapeBase> shapes)
        {
            EnsureDirectory();
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string json = JsonConvert.SerializeObject(shapes, jsonSettings);
                    File.WriteAllText(sfd.FileName, json);
                    MessageBox.Show("存檔成功！", "系統通知");
                }
            }
        }

        public static List<App_Shapes.ShapeBase> Load()
        {
            EnsureDirectory();
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Draw Project (*.draw)|*.draw", InitialDirectory = SaveDirectory })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string json = File.ReadAllText(ofd.FileName);
                    return JsonConvert.DeserializeObject<List<App_Shapes.ShapeBase>>(json, jsonSettings);
                }
            }
            return null;
        }

        // 用於複製貼上的深拷貝
        public static List<App_Shapes.ShapeBase> CloneShapes(List<App_Shapes.ShapeBase> shapes)
        {
            string json = JsonConvert.SerializeObject(shapes, jsonSettings);
            return JsonConvert.DeserializeObject<List<App_Shapes.ShapeBase>>(json, jsonSettings);
        }
    }
}
