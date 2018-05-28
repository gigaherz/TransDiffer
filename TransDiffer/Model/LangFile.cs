using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TransDiffer.Annotations;

namespace TransDiffer.Model
{
    public class LangFile : IExpandable
    {
        public ComponentFolder Folder { get; set; }
        public FileInfo File { get; set; }

        public ObservableCollection<SubLang> ContainedLangs { get; } = new ObservableCollection<SubLang>();

        public string Name => File.Name;
        public bool HasErrors => NamedLines.Values.Any(nl => nl.String.MissingInLanguages.Count > 0);
        public Brush Background => HasErrors ? Brushes.Pink : Brushes.Transparent;

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string[] Content;
        private FlowDocument cachedDocument;
        private FlowDocument cachedDetailsDocument;
        private bool _isExpanded;

        public void FinishLoading()
        {
            IsExpanded = HasErrors;
        }

        public Dictionary<int, TranslationStringReference> NamedLines { get; } = new Dictionary<int, TranslationStringReference>();

        public void BuildDocument(RichTextBox rtb, ToolTip tt, Action<int> progress)
        {
            if (cachedDocument != null)
            {
                rtb.Document = cachedDocument;
                return;
            }
#if false

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
#endif

            cachedDocument = new FlowDocument();
            rtb.Document = cachedDocument;
            cachedDocument.FontFamily = new FontFamily("Courier New");

            var range = new TextRange(cachedDocument.ContentStart, cachedDocument.ContentEnd);
            var fstream = new FileStream(File.FullName, FileMode.Open);
            
            range.Load(fstream, DataFormats.Text);
            range.ApplyPropertyValue(Block.MarginProperty, new Thickness());

            var firstLine = cachedDocument.ContentStart.GetLineStartPosition(0);

            foreach (var line in NamedLines.Values)
            {
                if (line.IdentifierToken != null && line.IdentifierToken.CompareTo(line.TextValueToken) > 0)
                {
                    SetColorIdentifier(firstLine, line);
                }

                if (line.TextValueToken != null)
                {
                    SetColorText(firstLine, line);
                }

                if (line.IdentifierToken != null && line.IdentifierToken.CompareTo(line.TextValueToken) < 0)
                {
                    SetColorIdentifier(firstLine, line);
                }
            }
        }

        private void SetColorText(TextPointer firstLine, TranslationStringReference line)
        {
            var TextBrush = new SolidColorBrush(Color.FromRgb(200, 255, 200));

            var start = firstLine
                .GetLineStartPosition(line.TextValueToken.Context.Line - 1)
                .GetPositionAtOffset(line.TextValueToken.Context.Column);
            var end = firstLine
                .GetLineStartPosition(line.TextValueToken.ContextEnd.Line - 1)
                .GetPositionAtOffset(line.TextValueToken.ContextEnd.Column);
            if (end == null)
            {
                end = cachedDocument.ContentEnd;
            }
            var range = new TextRange(start, end);
            range.ApplyPropertyValue(TextElement.BackgroundProperty, TextBrush);

            SetParagraphInfo(line, start, end);
        }

        private void SetColorIdentifier(TextPointer firstLine, TranslationStringReference line)
        {
            var IdBrush = new SolidColorBrush(Color.FromRgb(160, 255, 160));
            var IdBrushMissing = new SolidColorBrush(Color.FromRgb(255, 140, 140));

            var start = firstLine
                .GetLineStartPosition(line.IdentifierToken.Context.Line - 1)
                .GetPositionAtOffset(line.IdentifierToken.Context.Column);
            var end = firstLine
                .GetLineStartPosition(line.IdentifierToken.ContextEnd.Line - 1)
                .GetPositionAtOffset(line.IdentifierToken.ContextEnd.Column);
            if (end == null)
            {
                end = cachedDocument.ContentEnd;
            }
            var range = new TextRange(start, end);
            range.ApplyPropertyValue(TextElement.BackgroundProperty, line.String.MissingInLanguages.Count > 0 ? IdBrushMissing : IdBrush);

            SetParagraphInfo(line, start, end);
        }

        private static void SetParagraphInfo(TranslationStringReference line, TextPointer start, TextPointer end)
        {
            var startOfLine = start.GetLineStartPosition(0);
            while (startOfLine.GetOffsetToPosition(end) > 0)
            {
                startOfLine.Paragraph.Tag = line;
                startOfLine = startOfLine.GetLineStartPosition(1);
            }
        }

        public FlowDocument CreateDetailsDocument()
        {
            if (cachedDetailsDocument != null)
                return cachedDetailsDocument;

            var block = new Section();

            var containedLangs = new HashSet<string>(ContainedLangs.Select(s => s.Name));

            foreach(var str in Folder.NamedStrings)
            {
                if (str.MissingInLanguages.Any(s => containedLangs.Contains(s.Name)))
                {
                    var para = new Paragraph(new Run($"Missing {str.Name}, seen in: " + string.Join(", ", str.Translations.Select(e => $"{e.Key}({e.Value.Source.Name})")))) { Margin = new Thickness() };
                    block.Blocks.Add(para);
                }
            }

            cachedDetailsDocument = new FlowDocument(block);
            return cachedDetailsDocument;

        }

        public void InvalidateDocument() { cachedDocument = null; }
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}