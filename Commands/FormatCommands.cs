using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DrawingApp
{
    public class ChangeFormatCommand : ICommand
    {
        private class FormatState
        {
            public Color ShapeColor { get; set; }
            public Color FillColor { get; set; }
            
            public App_Shapes.BrushType FillBrushType { get; set; }
            public Color GradientColor2 { get; set; }
            public bool EnableShadow { get; set; }

            public float StrokeWidth { get; set; }
            public System.Drawing.Drawing2D.DashStyle StrokeDashStyle { get; set; }
            public string FontName { get; set; }
            public float FontSize { get; set; }
            public Color FontColor { get; set; }
            public bool FontBold { get; set; }
            public bool FontItalic { get; set; }
            public bool FontUnderline { get; set; }
            public App_Shapes.TextAlign TextAlignment { get; set; }

            public FormatState(App_Shapes.ShapeBase shape)
            {
                ShapeColor = shape.ShapeColor;
                FillColor = shape.FillColor;
                
                FillBrushType = shape.FillBrushType;
                GradientColor2 = shape.GradientColor2;
                EnableShadow = shape.EnableShadow;

                StrokeWidth = shape.StrokeWidth;
                StrokeDashStyle = shape.StrokeDashStyle;
                FontName = shape.FontName;
                FontSize = shape.FontSize;
                FontColor = shape.FontColor;
                FontBold = shape.FontBold;
                FontItalic = shape.FontItalic;
                FontUnderline = shape.FontUnderline;
                TextAlignment = shape.TextAlignment;
            }

            public void ApplyTo(App_Shapes.ShapeBase shape)
            {
                shape.ShapeColor = ShapeColor;
                shape.FillColor = FillColor;
                
                shape.FillBrushType = FillBrushType;
                shape.GradientColor2 = GradientColor2;
                shape.EnableShadow = EnableShadow;

                shape.StrokeWidth = StrokeWidth;
                shape.StrokeDashStyle = StrokeDashStyle;
                shape.FontName = FontName;
                shape.FontSize = FontSize;
                shape.FontColor = FontColor;
                shape.FontBold = FontBold;
                shape.FontItalic = FontItalic;
                shape.FontUnderline = FontUnderline;
                shape.TextAlignment = TextAlignment;
            }
        }

        private List<App_Shapes.ShapeBase> _shapes;
        private List<FormatState> _oldStates;
        private FormatState _newState; 

        public ChangeFormatCommand(List<App_Shapes.ShapeBase> shapes)
        {
            _shapes = shapes.ToList();
            _oldStates = shapes.Select(s => new FormatState(s)).ToList();
        }

        public void CaptureNewState()
        {
            _newState = new FormatState(_shapes[0]); 
        }

        public void Execute()
        {
            if (_newState != null)
            {
                foreach (var s in _shapes) _newState.ApplyTo(s);
            }
        }

        public void Undo()
        {
            for (int i = 0; i < _shapes.Count; i++)
            {
                _oldStates[i].ApplyTo(_shapes[i]);
            }
        }
    }
}
