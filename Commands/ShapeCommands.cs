using System.Collections.Generic;
using System.Linq;

namespace DrawingApp
{
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

    public class AddShapesCommand : ICommand
    {
        private List<App_Shapes.ShapeBase> _canvasShapes;
        private List<App_Shapes.ShapeBase> _shapes;

        public AddShapesCommand(List<App_Shapes.ShapeBase> canvasShapes, List<App_Shapes.ShapeBase> shapes)
        {
            _canvasShapes = canvasShapes;
            _shapes = shapes.ToList();
        }

        public void Execute() { _canvasShapes.AddRange(_shapes); }
        public void Undo() { foreach (var s in _shapes) _canvasShapes.Remove(s); }
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
        private int _direction; 

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
                if (_direction == 0) _canvasShapes.Add(s); 
                else if (_direction == -99) _canvasShapes.Insert(0, s); 
            }
        }

        public void Undo()
        {
            foreach (var s in _targetShapes) _canvasShapes.Remove(s);
            
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
