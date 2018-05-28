using System;

namespace TransDiffer.Parser.Exceptions
{
    [Serializable]
    public class ReaderException : LexerException
    {
        public ReaderException(IContextProvider context, string message)
            : base(context, message)
        {
        }
    }
}
