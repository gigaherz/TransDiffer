using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TransDiffer
{
    public class LangFile
    {
        public ComponentFolder Folder { get; set; }
        public FileInfo File { get; set; }

        public string Name => File.Name;
        public bool HasErrors => NamedLines.Values.Any(nl => nl.String.MissingInLanguages.Count > 0);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;
        public bool IsExpanded { get; set; }

        public string[] Content;
        private FlowDocument cachedDocument;

        public void FinishLoading()
        {
            IsExpanded = HasErrors;
        }

        public Dictionary<int, TranslationStringReference> NamedLines { get; } = new Dictionary<int, TranslationStringReference>();

        public FlowDocument BuildDocument(ToolTip tt, Action<int> progress)
        {
            if (cachedDocument != null)
                return cachedDocument;

            var block = new Section();

            progress(0);
            for (var i = 0; i < Content.Length; i++)
            {
                var line = Content[i];
                Paragraph para;
                var ll = NamedLines.TryGetValue(i, out var sl) ? sl : null;
                if (ll != null)
                {
                    para = ll.GetFormattedParagraph(tt);
                }
                else
                {
                    para = new Paragraph(new Run(line));
                }
                para.Margin = new Thickness();
                block.Blocks.Add(para);
                progress(i * 100 / (Content.Length - 1));
            }

            cachedDocument = new FlowDocument(block) { FontFamily = new FontFamily("Courier New") };
            return cachedDocument;
        }

        public void InvalidateDocument() { cachedDocument = null; }
    }
}