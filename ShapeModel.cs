// ==========================================
// 程式功能：定義繪圖物件的資料結構與邏輯屬性
// 對應選單：全域使用
// 對應資料庫：DrawingApp.db
// 對應資料表：App_CanvasEngine
// ==========================================
using System;
using System.Drawing;

namespace PortableDrawingApp
{
    public enum ShapeType { Line, Rectangle, Circle, Text, MindMapNode }

    public class ShapeModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ParentId { get; set; } = ""; 
        public string TypeStr { get; set; } = ShapeType.Rectangle.ToString();
        
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string BorderColorHex { get; set; } = Color.Black.ToArgb().ToString();
        public string Content { get; set; } = "";
        
        // 嚴格規範日期格式：寫入與讀取皆統一為 yyyy-MM-dd HH:mm:ss
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public ShapeType Type 
        { 
            get => (ShapeType)Enum.Parse(typeof(ShapeType), TypeStr); 
            set => TypeStr = value.ToString(); 
        }
        public Color BorderColor 
        { 
            get => Color.FromArgb(int.Parse(BorderColorHex)); 
            set => BorderColorHex = value.ToArgb().ToString(); 
        }
        public bool IsSelected { get; set; } = false;

        public RectangleF GetBounds() => new RectangleF(X, Y, Width, Height);

        public RectangleF[] GetResizeHandles()
        {
            float size = 8;
            return new RectangleF[] {
                new RectangleF(X - size/2, Y - size/2, size, size),
                new RectangleF(X + Width - size/2, Y - size/2, size, size),
                new RectangleF(X - size/2, Y + Height - size/2, size, size),
                new RectangleF(X + Width - size/2, Y + Height - size/2, size, size)
            };
        }
    }
}
