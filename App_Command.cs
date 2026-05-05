using System;
using System.Collections.Generic;
using System.Linq;

namespace DrawingApp
{
    // 命令介面
    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    // 歷史紀錄管理員
    public class CommandManager
    {
        private Stack<ICommand> _undoStack = new Stack<ICommand>();
        private Stack<ICommand> _redoStack = new Stack<ICommand>();

        public event Action OnStateChanged;

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // 執行新指令後，清除重做紀錄
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

    // --- 具體的命令實作 ---

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

        public void Execute() { foreach (var s in _shapesToRemove) _canvasShapes.Remove(s); }
        public void Undo() { foreach (var s in _shapesToRemove) _canvasShapes.Add(s); }
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

        public void Execute() { foreach (var s in _shapes) s.Move(_dx, _dy); }
        public void Undo() { foreach (var s in _shapes) s.Move(-_dx, -_dy); }
    }
}
