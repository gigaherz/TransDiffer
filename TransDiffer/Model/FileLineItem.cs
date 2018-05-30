using System.Collections.ObjectModel;
using System.Windows.Documents;

namespace TransDiffer.Model
{
    public class FileLineItem
    {
        public ObservableCollection<Inline> Inlines { get; } = new ObservableCollection<Inline>();
        public SourceInfo Tag { get; set; }
    }
}