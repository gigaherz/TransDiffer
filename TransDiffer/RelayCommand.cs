using System;
using System.Windows.Input;

namespace TransDiffer
{
    public class RelayCommand : ICommand
    {
        public Action<object> Target { get; }

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