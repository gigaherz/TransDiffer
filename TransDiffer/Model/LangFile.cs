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
        public bool HasErrors { get; private set; }
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
            foreach (var containedLang in ContainedLangs)
            {
                containedLang.FinishLoading();
            }

            IsExpanded = HasErrors;

            HasErrors = Folder.NamedStrings.Any(s => s.MissingInLanguages.Intersect(ContainedLangs).Any());
        }

        public Dictionary<int, TranslationStringReference> NamedLines { get; } = new Dictionary<int, TranslationStringReference>();

        private class StringOperation
        {
            public int line;
            public int startColumn;
            public int endColumn;
            public Brush colorToApply;
            public TranslationStringReference tag;

            public StringOperation(int line, int startColumn, int endColumn, Brush colorToApply, TranslationStringReference tag)
            {
                this.line = line;
                this.startColumn = startColumn;
                this.endColumn = endColumn;
                this.colorToApply = colorToApply;
                this.tag = tag;
            }
        }

        public void BuildDocument(RichTextBox rtb, ToolTip tt, Action<int> progress)
        {
            if (cachedDocument != null)
            {
                rtb.Document = cachedDocument;
                return;
            }

            var lines = System.IO.File.ReadAllLines(File.FullName);

            var TextBrush = new SolidColorBrush(Color.FromRgb(200, 255, 200));
            var IdBrush = new SolidColorBrush(Color.FromRgb(160, 255, 160));
            var IdBrushMissing = new SolidColorBrush(Color.FromRgb(255, 180, 140));

            List< StringOperation> operations = new List<StringOperation>();
            foreach (var line in NamedLines.Values)
            {
                if (line.IdentifierToken != null)
                {
                    var brush = line.String.MissingInLanguages.Count > 0 ? IdBrushMissing : IdBrush;
                    int line0 = line.IdentifierToken.Context.Line-1;
                    int col0 = line.IdentifierToken.Context.Column-1;
                    int line1 = line.IdentifierToken.ContextEnd.Line-1;
                    int col1 = line.IdentifierToken.ContextEnd.Column-1;
                    if (line0 == line1)
                    {
                        operations.Add(new StringOperation(line0, col0, col1, brush, line));
                    }
                    else
                    {
                        var colT = lines[line0].Length;
                        operations.Add(new StringOperation(line0, col0, colT, brush, line));
                        for(int i=line0+1;i<line1;i++)
                        {
                            colT = lines[i].Length;
                            operations.Add(new StringOperation(i, 0, colT, brush, line));
                        }
                        operations.Add(new StringOperation(line1, 0, col1, brush, line));
                    }
                }

                if (line.TextValueToken != null)
                {
                    var brush = TextBrush;
                    int line0 = line.TextValueToken.Context.Line-1;
                    int col0 = line.TextValueToken.Context.Column-1;
                    int line1 = line.TextValueToken.ContextEnd.Line-1;
                    int col1 = line.TextValueToken.ContextEnd.Column-1;
                    if (line0 == line1)
                    {
                        operations.Add(new StringOperation(line0, col0, col1, brush, line));
                    }
                    else
                    {
                        var colT = lines[line0].Length;
                        operations.Add(new StringOperation(line0, col0, colT, brush, line));
                        for (int i = line0 + 1; i < line1; i++)
                        {
                            colT = lines[i].Length;
                            operations.Add(new StringOperation(i, 0, colT, brush, line));
                        }
                        operations.Add(new StringOperation(line1, 0, col1, brush, line));
                    }
                }
            }

            operations.Sort((a,b) => {
                int t = a.line.CompareTo(b.line);
                if (t != 0) return t;
                return a.startColumn.CompareTo(b.startColumn);
            });
            
            cachedDocument = new FlowDocument();
            rtb.Document = cachedDocument;
            cachedDocument.FontFamily = new FontFamily("Courier New");

            var section = new Section();
            cachedDocument.Blocks.Add(section);

            SourceInfo previousSourceInfo = null;
            int j = 0;
            for(int i=0;i<lines.Length;i++)
            {
                var sourceInfo = new SourceInfo() { File = File, Line = i + 1 };
                sourceInfo.Previous = previousSourceInfo;
                if (previousSourceInfo != null)
                    previousSourceInfo.Next = sourceInfo;
                var para = new Paragraph()
                {
                    Margin = new Thickness(),
                    Tag = sourceInfo
                };
                var ln = lines[i];
                int col = 0;
                int end = ln.Length;
                while(j < operations.Count && operations[j].line == i)
                {
                    var op = operations[j++];
                    if (op.startColumn > col)
                    {
                        para.Inlines.Add(new Run(ln.Substring(col, op.startColumn - col)));
                    }

                    para.Inlines.Add(new Run(ln.Substring(op.startColumn, op.endColumn - op.startColumn)) { Background = op.colorToApply });
                    col = op.endColumn;
                    sourceInfo.Strings.Add(op.tag);
                    op.tag.Paragraphs.Add(para);
                }

                if (end > col)
                {
                    para.Inlines.Add(new Run(ln.Substring(col, end - col)));
                }

                section.Blocks.Add(para);
                previousSourceInfo = sourceInfo;
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
                    var list = string.Join(", ", str.Translations.Select(e => $"{e.Key}({e.Value.Source.Name})"));
                    var para = new Paragraph() { Margin = new Thickness() };
                    para.Inlines.Add(new Run($"Missing {str.Name}, seen in: {list}"));
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