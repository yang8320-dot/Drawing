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
        // 優化：使用 LinkedList 取代 Stack，以實作最大步數限制，避免記憶體溢出
        private LinkedList<ICommand> _undoStack = new LinkedList<ICommand>();
        private LinkedList<ICommand> _redoStack = new LinkedList<ICommand>();
        
        // 設定最大復原步數
        public int MaxUndoSteps { get; set; } = 50;

        public event Action OnStateChanged;

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            
            _undoStack.AddLast(command);
            
            // 優化：如果超過最大步數，捨棄最舊的紀錄釋放記憶體
            if (_undoStack.Count > MaxUndoSteps)
            {
                _undoStack.RemoveFirst();
            }

            _redoStack.Clear(); 
            OnStateChanged?.Invoke();
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Last.Value;
                _undoStack.RemoveLast();
                
                command.Undo();
                
                _redoStack.AddLast(command);
                OnStateChanged?.Invoke();
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Last.Value;
                _redoStack.RemoveLast();
                
                command.Execute();
                
                _undoStack.AddLast(command);
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

    public class ChangeZIndexCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private List<App_Shapes.ShapeBase> _targetShapes;
        private List<int> _oldIndices;
        private int _direction; // 0 = 頂層, -99 = 底層

        public ChangeZIndexCommand(List<App_Shapes.ShapeBase> canvasShapes, List<App_Shapes.ShapeBase> targetShapes, int direction)
        {
            _canvasShapes = canvasShapes;
            _targetShapes = targetShapes.ToList();
            _direction = direction;
            
            _oldIndices = new List<int>();
            foreach (var s in _targetShapes)
            {
                _oldIndices.Add(_canvasShapes.IndexOf(s));
            }
        }

        public void Execute()
        {
            foreach (var s in _targetShapes)
            {
                _canvasShapes.Remove(s);
                if (_direction == 0) _canvasShapes.Add(s); // 移到最後 (最上層)
                else if (_direction == -99) _canvasShapes.Insert(0, s); // 移到最前 (最下層)
            }
        }

        public void Undo()
        {
            foreach (var s in _targetShapes) _canvasShapes.Remove(s);
            
            // 根據原本的索引重新插入，確保順序從大到小插入避免跑位
            var restoreList = _targetShapes.Zip(_oldIndices, (shape, index) => new { shape, index }).OrderBy(x => x.index).ToList();
            foreach (var item in restoreList)
            {
                if (item.index >= 0 && item.index <= _canvasShapes.Count)
                    _canvasShapes.Insert(item.index, item.shape);
                else
                    _canvasShapes.Add(item.shape);
            }
        }
    }
}
