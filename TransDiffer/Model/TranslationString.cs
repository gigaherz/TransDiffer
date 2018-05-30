using System;
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
        private ObservableCollection<FileLineItem> cachedDetailsDocument;

        public string Name { get; set; }
        public ObservableCollection<TranslationStringReference> Lines { get; } = new ObservableCollection<TranslationStringReference>();

        public Dictionary<string, TranslationStringReference> Translations { get; } = new Dictionary<string, TranslationStringReference>();

        public ObservableCollection<SubLang> MissingInLanguages { get; } = new ObservableCollection<SubLang>();

        public TranslationString Parent { get; set; }

        public bool HasErrors { get; set; }
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<FileLineItem> CreateDetailsDocument(Action<TranslationStringReference> navigateToLine, Action<LangFile> navigateToFile)
        {
            if (cachedDetailsDocument != null)
                return cachedDetailsDocument;

            cachedDetailsDocument = new ObservableCollection<FileLineItem>();

            var para = new FileLineItem();
            para.Inlines.Add(new Run($"Internal ID: {Name}"));
            cachedDetailsDocument.Add(para);

            if (Translations.Count > 0)
            {
                para = new FileLineItem();

                para.Inlines.Add(new Run("Translated to: "));

                bool first = true;
                foreach (var t in Translations)
                {
                    if (!first)
                    {
                        para.Inlines.Add(new Run(", "));
                    }

                    var tr = t.Key;
                    var file = t.Value;

                    var link = new Hyperlink(new Run(tr));
                    para.Inlines.Add(link);
                    para.Inlines.Add(new Run($"({file.Source.Name})"));

                    first = false;

                    link.Click += (s, a) =>
                        navigateToLine(t.Value);
                }

                cachedDetailsDocument.Add(para);
            }

            if (MissingInLanguages.Count > 0)
            {
                para = new FileLineItem();
                para.Inlines.Add(new Run("Missing in: "));

                bool first = true;
                foreach (var t in MissingInLanguages)
                {
                    if (!first)
                    {
                        para.Inlines.Add(new Run(", "));
                    }

                    var tr = t.Name;
                    var file = t;

                    var link = new Hyperlink(new Run(tr));
                    para.Inlines.Add(link);
                    para.Inlines.Add(new Run($"({file.Source.Name})"));

                    first = false;

                    bool found = false;
                    var tParent = this;
                    while (tParent.Parent != null)
                    {
                        tParent = tParent.Parent;
                        if (tParent.Translations.TryGetValue(tParent.Name, out var pt))
                        {
                            link.Click += (s, a) =>
                                navigateToLine(pt);
                            found = true;
                            break;
                        }
                    }

                    if(!found)
                    {
                        link.Click += (s, a) =>
                            navigateToFile(t.Source);
                    }
                }

                cachedDetailsDocument.Add(para);
            }

            return cachedDetailsDocument;
        }
    }
}