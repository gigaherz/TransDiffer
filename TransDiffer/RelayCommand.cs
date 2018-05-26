using System;
using System.Windows.Input;

namespace TransDiffer
{
    public class RelayCommand : ICommand
    {
        private Action<object> _target;

        public Action<object> Target
        {
            get => _target;
            set
            {
                if (_target == value) return;
                _target = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public RelayCommand(Action<object> target)
        {
            Target = target;
        }

        public bool CanExecute(object parameter)
        {
            return Target != null;
        }

        public void Execute(object parameter)
        {
            Target?.Invoke(parameter);
        }

        public event EventHandler CanExecuteChanged;
    }
}