
namespace TransDiffer.Parser
{
    public class ParsingContext
    {
        public readonly string Filename;
        public readonly int Line;
        public readonly int Column;

        public ParsingContext(string f, int l, int c)
        {
            Filename = f;
            Line = l;
            Column = c;
        }

        public override string ToString()
        {
            return $"{Filename}({Line},{Column})";
        }
    }
}
