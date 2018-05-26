using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace TransDiffer
{
    public class TranslationStringReference
    {
        public LangFile Source { get; set; }
        public int LineNumber { get; set; }
        public Match RegexMatch { get; set; }

        public string Id { get; set; }
        public string Language { get; set; }

        // Results of the comparison
        public TranslationString String { get; set; }

        public override string ToString()
        {
            return $"{{{RegexMatch.Groups["text"].Value}}}";
        }

        public Paragraph GetFormattedParagraph(ToolTip tt)
        {
            var LineBrush = new SolidColorBrush(Color.FromRgb(245, 255, 245));
            var IdBrush = new SolidColorBrush(Color.FromRgb(160, 255, 160));
            var TextBrush = new SolidColorBrush(Color.FromRgb(200, 255, 200));
            var IdBrushMissing = new SolidColorBrush(Color.FromRgb(255, 140, 140));

            var whole = RegexMatch.Value;
            var id = RegexMatch.Groups["id"];
            var text = RegexMatch.Groups["text"];

            var para = new Paragraph { Background = LineBrush };

            if (!id.Success)
            {
                para.Inlines.Add(new Run(whole.Substring(0, text.Index)));
                para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length)));
            }
            else
            {
                if (id.Index < text.Index)
                {
                    para.Inlines.Add(new Run(whole.Substring(0, id.Index)));
                    para.Inlines.Add(new Run(id.Value) { Background = IdBrush });
                    para.Inlines.Add(new Run(whole.Substring(id.Index + id.Length, text.Index - id.Index - id.Length)));
                    para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                    para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length)));
                }
                else
                {
                    para.Inlines.Add(new Run(whole.Substring(0, text.Index)));
                    para.Inlines.Add(new Run(text.Value) { Background = TextBrush });
                    para.Inlines.Add(new Run(whole.Substring(text.Index + text.Length, id.Index - text.Index - text.Length)));
                    para.Inlines.Add(new Run(id.Value) { Background = IdBrush });
                    para.Inlines.Add(new Run(whole.Substring(id.Index + id.Length)));
                }
            }

            if (String.MissingInLanguages.Count > 0)
            {
                para.Background = IdBrushMissing;
                para.MouseEnter += (sender, args) =>
                {
                    if (args.LeftButton == MouseButtonState.Pressed
                        || args.RightButton == MouseButtonState.Pressed
                        || args.MiddleButton == MouseButtonState.Pressed
                        || args.XButton1 == MouseButtonState.Pressed
                        || args.XButton2 == MouseButtonState.Pressed)
                        return;
                    tt.Content = $"Detected missing strings for {Id}: {string.Join(", ", String.MissingInLanguages.Select(o => $"{o.Name}({o.Source.Name})"))}";
                    tt.IsOpen = true;
                };
                para.MouseLeave += (sender, args) =>
                {
                    tt.IsOpen = false;
                };
            }
            else
            {
                para.MouseEnter += (sender, args) =>
                {
                    if (args.LeftButton == MouseButtonState.Pressed
                        || args.RightButton == MouseButtonState.Pressed
                        || args.MiddleButton == MouseButtonState.Pressed
                        || args.XButton1 == MouseButtonState.Pressed
                        || args.XButton2 == MouseButtonState.Pressed)
                        return;
                    tt.Content = $"Id: {Id}";
                    tt.IsOpen = true;
                };
                para.MouseLeave += (sender, args) =>
                {
                    tt.IsOpen = false;
                };
            }

            return para;
        }
    }
}