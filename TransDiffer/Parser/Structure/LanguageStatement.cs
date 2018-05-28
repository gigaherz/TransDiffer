namespace TransDiffer.Parser.Structure
{
    public class LanguageStatement : ResourceStatement
    {
        public Token Lang { get; set; }
        public Token SubLang { get; set; }
    }
}