using System.ComponentModel;

namespace TransDiffer.Model
{
    interface IExpandable : INotifyPropertyChanged
    {
        bool IsExpanded { get; }
    }
}
