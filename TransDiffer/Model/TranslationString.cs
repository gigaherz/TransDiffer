using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace TransDiffer
{
    public class TranslationString
    {
        public string Name { get; set; }
        public ObservableCollection<TranslationStringReference> Lines { get; } = new ObservableCollection<TranslationStringReference>();

        public Dictionary<string, TranslationStringReference> Translations { get; } = new Dictionary<string, TranslationStringReference>();

        public ObservableCollection<SubLang> MissingInLanguages { get; } = new ObservableCollection<SubLang>();

        public bool HasErrors { get; set; }
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }
    }
}