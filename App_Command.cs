using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DrawingApp
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    public class CommandManager
    {
        private Stack<ICommand> _undoStack = new Stack<ICommand>();
        private Stack<ICommand> _redoStack = new Stack<ICommand>();

        public event Action OnStateChanged;

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); 
            OnStateChanged?.Invoke();
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                OnStateChanged?.Invoke();
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                OnStateChanged?.Invoke();
            }
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
    }

    public class AddShapeCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private App_Shapes.ShapeBase _shape;

        public AddShapeCommand(List<App_Shapes.ShapeBase> canvasShapes, App_Shapes.ShapeBase shape)
        {
            _canvasShapes = canvasShapes;
            _shape = shape;
        }

        public void Execute() { _canvasShapes.Add(_shape); }
        public void Undo() { _canvasShapes.Remove(_shape); }
    }

    public class RemoveShapesCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private List<App_Shapes.ShapeBase> _shapesToRemove;

        public RemoveShapesCommand(List<App_Shapes.ShapeBase> canvasShapes, List<App_Shapes.ShapeBase> shapesToRemove)
        {
            _canvasShapes = canvasShapes;
            _shapesToRemove = shapesToRemove.ToList();
        }

        public void Execute() 
        { 
            foreach (var s in _shapesToRemove) _canvasShapes.Remove(s); 
        }
        public void Undo() 
        { 
            foreach (var s in _shapesToRemove) _canvasShapes.Add(s); 
        }
    }

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

    // --- 新增：調整連線端點指令 ---
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

    public class GroupCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private List<App_Shapes.ShapeBase> _shapesToGroup;
        private App_Shapes.GroupShape _groupShape;

        public GroupCommand(List<App_Shapes.ShapeBase> canvasShapes, List<App_Shapes.ShapeBase> shapesToGroup, App_Shapes.GroupShape groupShape)
        {
            _canvasShapes = canvasShapes;
            _shapesToGroup = shapesToGroup.ToList();
            _groupShape = groupShape;
        }

        public void Execute()
        {
            foreach (var s in _shapesToGroup) _canvasShapes.Remove(s);
            _canvasShapes.Add(_groupShape);
        }

        public void Undo()
        {
            _canvasShapes.Remove(_groupShape);
            foreach (var s in _shapesToGroup) _canvasShapes.Add(s);
        }
    }

    public class UngroupCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private App_Shapes.GroupShape _groupShape;
        private List<App_Shapes.ShapeBase> _children;

        public UngroupCommand(List<App_Shapes.ShapeBase> canvasShapes, App_Shapes.GroupShape groupShape)
        {
            _canvasShapes = canvasShapes;
            _groupShape = groupShape;
            _children = groupShape.Children.ToList();
        }

        public void Execute()
        {
            _canvasShapes.Remove(_groupShape);
            foreach (var s in _children) _canvasShapes.Add(s);
        }

        public void Undo()
        {
            foreach (var s in _children) _canvasShapes.Remove(s);
            _canvasShapes.Add(_groupShape);
        }
    }
}
