namespace TransDiffer.Parser.Config
{
    public class StringGenerationContext
    {
        public StringGenerationOptions Options;

        public int IndentLevel = 1;

        public StringGenerationContext(StringGenerationOptions options)
        {
            Options = options;
        }
    }
}