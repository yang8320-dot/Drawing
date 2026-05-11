using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml;

namespace DrawingApp
{
    public static class App_SvgParser
    {
        public static List<App_Shapes.ShapeBase> ParseSvg(string filePath)
        {
            List<App_Shapes.ShapeBase> shapes = new List<App_Shapes.ShapeBase>();

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlNodeList elements = doc.SelectNodes("//*[local-name()='rect' or local-name()='ellipse' or local-name()='line' or local-name()='text' or local-name()='polygon']");
                
                foreach (XmlNode node in elements)
                {
                    try
                    {
                        var shape = CreateShapeFromNode(node);
                        if (shape != null) shapes.Add(shape);
                    }
                    catch { /* 忽略個別解析失敗的圖形 */ }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SVG 解析失敗: " + ex.Message);
                return null;
            }

            return shapes;
        }

        private static App_Shapes.ShapeBase CreateShapeFromNode(XmlNode node)
        {
            string name = node.LocalName.ToLower();
            
            Color strokeColor = ParseColor(GetAttr(node, "stroke"), Color.Black);
            Color fillColor = ParseColor(GetAttr(node, "fill"), Color.Transparent);
            float strokeWidth = ParseFloat(GetAttr(node, "stroke-width"), 2f);
            
            App_Shapes.ShapeBase shape = null;

            if (name == "rect")
            {
                float x = ParseFloat(GetAttr(node, "x"));
                float y = ParseFloat(GetAttr(node, "y"));
                float w = ParseFloat(GetAttr(node, "width"));
                float h = ParseFloat(GetAttr(node, "height"));
                
                if (HasAttr(node, "rx"))
                    shape = new App_Shapes.RoundedRectShape(new PointF(x, y), strokeColor);
                else
                    shape = new App_Shapes.RectShape(new PointF(x, y), strokeColor);

                shape.Bounds = new RectangleF(x, y, w, h);
            }
            else if (name == "ellipse")
            {
                float cx = ParseFloat(GetAttr(node, "cx"));
                float cy = ParseFloat(GetAttr(node, "cy"));
                float rx = ParseFloat(GetAttr(node, "rx"));
                float ry = ParseFloat(GetAttr(node, "ry"));

                shape = new App_Shapes.CircleShape(new PointF(cx - rx, cy - ry), strokeColor);
                shape.Bounds = new RectangleF(cx - rx, cy - ry, rx * 2, ry * 2);
            }
            else if (name == "line")
            {
                float x1 = ParseFloat(GetAttr(node, "x1"));
                float y1 = ParseFloat(GetAttr(node, "y1"));
                float x2 = ParseFloat(GetAttr(node, "x2"));
                float y2 = ParseFloat(GetAttr(node, "y2"));

                var conn = new App_Shapes.ConnectorShape(new PointF(x1, y1), strokeColor, false, false);
                conn.UpdateEndPoint(new PointF(x2, y2));
                shape = conn;
            }
            else if (name == "text")
            {
                float x = ParseFloat(GetAttr(node, "x"));
                float y = ParseFloat(GetAttr(node, "y"));
                string text = node.InnerText;
                
                shape = new App_Shapes.TextNodeShape(new PointF(x, y), strokeColor, true);
                shape.Text = text;
                shape.FontColor = ParseColor(GetAttr(node, "fill"), Color.Black);
                shape.FontSize = ParseFloat(GetAttr(node, "font-size"), 12f);
                shape.Bounds = new RectangleF(x - 50, y - 10, 100, 20); // 預估大小
            }

            if (shape != null)
            {
                shape.FillColor = fillColor;
                shape.StrokeWidth = strokeWidth;

                string transform = GetAttr(node, "transform");
                if (transform.Contains("rotate"))
                {
                    string angleStr = transform.Replace("rotate(", "").Split(',')[0].Replace(")", "");
                    shape.RotationAngle = ParseFloat(angleStr);
                }
            }

            return shape;
        }

        private static string GetAttr(XmlNode node, string attrName) => node.Attributes[attrName]?.Value ?? "";
        private static bool HasAttr(XmlNode node, string attrName) => node.Attributes[attrName] != null;
        
        private static float ParseFloat(string val, float defaultVal = 0)
        {
            if (string.IsNullOrEmpty(val)) return defaultVal;
            val = val.Replace("px", "").Trim();
            return float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float result) ? result : defaultVal;
        }

        private static Color ParseColor(string val, Color defaultColor)
        {
            if (string.IsNullOrEmpty(val) || val == "none") return Color.Transparent;
            try
            {
                if (val.StartsWith("#")) return ColorTranslator.FromHtml(val);
                return Color.FromName(val);
            }
            catch { return defaultColor; }
        }
    }
}
