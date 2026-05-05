using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace DrawingApp
{
    public static class App_SaveLoad
    {
        private static string SaveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save");

        // Newtonsoft.Json 的神奇設定，能保留多型 (繼承關係)
        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented
        };

        static App_SaveLoad()
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
        }

        public static void SaveAs(List<App_Shapes.ShapeBase> shapes)
        {
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
    }
}
