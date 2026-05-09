using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DrawingApp
{
    public class MoveShapesCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _shapes;
        private float _dx;
        private float _dy;

        public MoveShapesCommand(List<App_Shapes.ShapeBase> shapes, float dx, float dy)
        {
            _shapes = shapes.ToList();
            _dx = dx;
            _dy = dy;
        }

        public void Execute() 
        { 
            foreach (var s in _shapes) s.Move(_dx, _dy); 
        }
        public void Undo() 
        { 
            foreach (var s in _shapes) s.Move(-_dx, -_dy); 
        }
    }

    public class ResizeShapeCommand : ICommand
    {
        private App_Shapes.ShapeBase _shape;
        private RectangleF _oldBounds;
        private RectangleF _newBounds;

        public ResizeShapeCommand(App_Shapes.ShapeBase shape, RectangleF oldBounds, RectangleF newBounds)
        {
            _shape = shape;
            _oldBounds = oldBounds;
            _newBounds = newBounds;
        }

        public void Execute() { _shape.SetBounds(_newBounds); }
        public void Undo() { _shape.SetBounds(_oldBounds); }
    }

    public class TransformShapesCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _shapes;
        private List<RectangleF> _oldBounds;
        private List<RectangleF> _newBounds;

        public TransformShapesCommand(List<App_Shapes.ShapeBase> shapes, List<RectangleF> oldBounds, List<RectangleF> newBounds)
        {
            _shapes = shapes.ToList();
            _oldBounds = oldBounds.ToList();
            _newBounds = newBounds.ToList();
        }

        public void Execute()
        {
            for (int i = 0; i < _shapes.Count; i++) _shapes[i].SetBounds(_newBounds[i]);
        }

        public void Undo()
        {
            for (int i = 0; i < _shapes.Count; i++) _shapes[i].SetBounds(_oldBounds[i]);
        }
    }

    public class RotateShapeCommand : ICommand
    {
        private App_Shapes.ShapeBase _shape;
        private float _oldAngle;
        private float _newAngle;

        public RotateShapeCommand(App_Shapes.ShapeBase shape, float oldAngle, float newAngle)
        {
            _shape = shape;
            _oldAngle = oldAngle;
            _newAngle = newAngle;
        }

        public void Execute() { _shape.RotationAngle = _newAngle; }
        public void Undo() { _shape.RotationAngle = _oldAngle; }
    }

    public class AdjustConnectorCommand : ICommand
    {
        private App_Shapes.ConnectorShape _conn;
        private Guid _oldSrcId, _oldTgtId, _newSrcId, _newTgtId;
        private App_Shapes.AnchorPosition _oldSA, _oldTA, _newSA, _newTA;
        private PointF _oldStart, _oldEnd, _newStart, _newEnd;

        public AdjustConnectorCommand(
            App_Shapes.ConnectorShape conn,
            Guid oldSrcId, Guid oldTgtId, App_Shapes.AnchorPosition oldSA, App_Shapes.AnchorPosition oldTA, PointF oldStart, PointF oldEnd,
            Guid newSrcId, Guid newTgtId, App_Shapes.AnchorPosition newSA, App_Shapes.AnchorPosition newTA, PointF newStart, PointF newEnd)
        {
            _conn = conn;
            _oldSrcId = oldSrcId; _oldTgtId = oldTgtId; _oldSA = oldSA; _oldTA = oldTA; _oldStart = oldStart; _oldEnd = oldEnd;
            _newSrcId = newSrcId; _newTgtId = newTgtId; _newSA = newSA; _newTA = newTA; _newStart = newStart; _newEnd = newEnd;
        }

        public void Execute()
        {
            _conn.SourceId = _newSrcId; _conn.TargetId = _newTgtId;
            _conn.SourceAnchor = _newSA; _conn.TargetAnchor = _newTA;
            _conn.StartPt = _newStart; _conn.EndPt = _newEnd;
        }

        public void Undo()
        {
            _conn.SourceId = _oldSrcId; _conn.TargetId = _oldTgtId;
            _conn.SourceAnchor = _oldSA; _conn.TargetAnchor = _oldTA;
            _conn.StartPt = _oldStart; _conn.EndPt = _oldEnd;
        }
    }
}
