using TransDiffer.Parser;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Model
{
    public class TranslationStringReference
    {
        public LangFile Source { get; set; }
        public ParsingContext Context { get; set; }
        public ExpressionValue IdentifierToken { get; set; }
        public Token TextValueToken { get; set; }

        public string Id { get; set; }
        public string Language { get; set; }

        // Results of the comparison
        public TranslationString String { get; set; }

        public override string ToString()
        {
            return $"{{{IdentifierToken.Process()}={Lexer.UnescapeString(TextValueToken)}}}";
        }

#if false
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
            para.Tag = this;

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
#endif
    }
}