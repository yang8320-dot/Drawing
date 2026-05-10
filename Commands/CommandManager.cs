using System;
using System.Collections.Generic;

namespace DrawingApp
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    public class CommandManager
    {
        private LinkedList<ICommand> _undoStack = new LinkedList<ICommand>();
        private LinkedList<ICommand> _redoStack = new LinkedList<ICommand>();
        
        public int MaxUndoSteps { get; set; } = 50;
        public event Action OnStateChanged;

        public void ExecuteCommand(ICommand command)
        {
            command.Execute();
            
            _undoStack.AddLast(command);
            
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
}
