using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using TransDiffer.Annotations;

namespace TransDiffer.Model
{
    public class TranslationString : IExpandable
    {
        private FlowDocument cachedDocument;

        public string Name { get; set; }
        public ObservableCollection<TranslationStringReference> Lines { get; } = new ObservableCollection<TranslationStringReference>();

        public Dictionary<string, TranslationStringReference> Translations { get; } = new Dictionary<string, TranslationStringReference>();

        public ObservableCollection<SubLang> MissingInLanguages { get; } = new ObservableCollection<SubLang>();

        public bool HasErrors { get; set; }
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public FlowDocument CreateDetailsDocument()
        {
            if (cachedDocument != null)
                return cachedDocument;

            var block = new Section();

            var para = new Paragraph(new Run($"Internal ID: {Name}")) { Margin = new Thickness() };
            block.Blocks.Add(para);

            if (Translations.Count > 0)
            {
                para = new Paragraph(new Run("Translated to: " + string.Join(", ", Translations.Select(e => $"{e.Key}({e.Value.Source.Name})")))) { Margin = new Thickness() };
                block.Blocks.Add(para);
            }

            if (MissingInLanguages.Count > 0)
            {
                para = new Paragraph(new Run("Missing in: " + string.Join(", ", MissingInLanguages.Select(o => $"{o.Name}({o.Source.Name})")))) { Margin = new Thickness() };
                block.Blocks.Add(para);
            }

            cachedDocument = new FlowDocument(block);
            return cachedDocument;
        }
    }
}