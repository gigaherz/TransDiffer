using System;
using System.Text;
using TransDiffer.Parser.Exceptions;
using TransDiffer.Parser.Util;

namespace TransDiffer.Parser
{
    public class Lexer : IContextProvider, IDisposable
    {
        readonly QueueList<Token> lookAhead = new QueueList<Token>();

        readonly Reader reader;

        bool seenEnd = false;

        public Lexer(Reader r)
        {
            reader = r;
        }

        private void Require(int count)
        {
            int needed = count - lookAhead.Count;
            if (needed > 0)
            {
                ReadAhead(needed);
            }
        }

        public Tokens Peek(int pos = 0)
        {
            Require(pos + 1);

            return lookAhead[pos].Name;
        }

        public Token PeekToken(int pos = 0)
        {
            Require(pos + 1);

            return lookAhead[pos];
        }

        public Token Pop()
        {
            Require(2);

            return lookAhead.Remove();
        }

        private void ReadAhead(int needed)
        {
            while (needed-- > 0)
            {
                lookAhead.Add(ParseOne());
            }
        }

        Tuple<string, Tokens>[] keywords =
        {
            Tuple.Create("Begin", Tokens.Begin),
            Tuple.Create("End", Tokens.IdEnd),
            Tuple.Create("Language", Tokens.Language),
            Tuple.Create("Menu", Tokens.Menu),
            Tuple.Create("MenuEx", Tokens.MenuEx),
            Tuple.Create("Popup", Tokens.Popup),
            Tuple.Create("MenuItem", Tokens.MenuItem),
            Tuple.Create("Separator", Tokens.Separator),
            Tuple.Create("Dialog", Tokens.Dialog),
            Tuple.Create("DialogEx", Tokens.DialogEx),
            Tuple.Create("Style", Tokens.Style),
            Tuple.Create("Caption", Tokens.Caption),
            Tuple.Create("Font", Tokens.Font),
            Tuple.Create("Control", Tokens.Control),
            Tuple.Create("EditText", Tokens.EditText),
            Tuple.Create("DefPushButton", Tokens.DefPushButton),
            Tuple.Create("PushButton", Tokens.PushButton),
            Tuple.Create("LText", Tokens.LText),
            Tuple.Create("GroupBox", Tokens.GroupBox),
            Tuple.Create("StringTable", Tokens.StringTable),
            Tuple.Create("Discardable", Tokens.Discardable),
            Tuple.Create("ListBox", Tokens.ListBox),
            Tuple.Create("CheckBox", Tokens.CheckBox),
            Tuple.Create("RadioButton", Tokens.RadioButton),
            Tuple.Create("RText", Tokens.RText),
            Tuple.Create("AutoRadioButton", Tokens.AutoRadioButton),
            Tuple.Create("AutoCheckBox", Tokens.AutoCheckBox),
            Tuple.Create("Icon", Tokens.Icon),
            Tuple.Create("ComboBox", Tokens.ComboBox),
            Tuple.Create("CText", Tokens.CText),
            Tuple.Create("And", Tokens.And),
            Tuple.Create("Or", Tokens.Or),
            Tuple.Create("Not", Tokens.Not),
        };

        private Token ParseOne()
        {
            if (seenEnd)
                return new Token(Tokens.End, reader.GetParsingContext(), "", reader.GetParsingContext());

            int ich = reader.Peek();
            while (true)
            {
                if (ich < 0)
                {
                    seenEnd = true;
                    return new Token(Tokens.End, reader.GetParsingContext(), "", reader.GetParsingContext());
                }

                switch (ich)
                {
                case ' ':
                case '\t':
                case '\r':
                case '\n':
                    reader.Skip(1);

                    ich = reader.Peek();
                    break;
                case '#':
                    // compiler directive, ignore for now (until end of line)
                    do
                    {
                        reader.Skip(1);

                        ich = reader.Peek();
                    }
                    while (ich > 0 && ich != '\n' && ich != '\r');
                    break;
                case '/':
                    var ich2 = reader.Peek(1);
                    switch (ich2)
                    {
                    case '/':
                        // comment, Skip until \r or \n
                        do
                        {
                            reader.Skip(1);

                            ich = reader.Peek();
                        }
                        while (ich > 0 && ich != '\n' && ich != '\r');
                        break;
                    case '*':
                        // comment, Skip until '*/'
                        do
                        {
                            reader.Skip(1);

                            ich = reader.Peek();
                            ich2 = reader.Peek(1);
                        }
                        while (ich > 0 && (ich != '*' || ich2 != '/'));
                        reader.Skip(2);
                        ich = reader.Peek();
                        break;
                    default:
                        goto blah;
                    }
                    break;
                default:
                    goto blah;
                }
            }

            blah:
            switch (ich)
            {
            case '{': return new Token(Tokens.LBrace, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '}': return new Token(Tokens.RBrace, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '(': return new Token(Tokens.LParen, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case ')': return new Token(Tokens.RParen, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case ',': return new Token(Tokens.Comma, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '+': return new Token(Tokens.Plus, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '-': return new Token(Tokens.Minus, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '*': return new Token(Tokens.Asterisk, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '/': return new Token(Tokens.Slash, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '|': return new Token(Tokens.Pipe, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '&': return new Token(Tokens.Ampersand, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            case '~': return new Token(Tokens.Squiggly, reader.GetParsingContext(), reader.Read(1), reader.GetParsingContext());
            }

            if (char.IsLetter((char)ich) || ich == '_')
            {
                int number = 1;
                while (true)
                {
                    ich = reader.Peek(number);
                    if (ich < 0)
                        break;

                    if (char.IsLetter((char)ich) || char.IsDigit((char)ich) || ich == '_')
                    {
                        number++;
                    }
                    else
                    {
                        break;
                    }
                }

                var id = new Token(Tokens.Ident, reader.GetParsingContext(), reader.Read(number), reader.GetParsingContext());

                foreach (var keyword in keywords)
                {
                    if (string.Compare(id.Text, keyword.Item1, StringComparison.OrdinalIgnoreCase) == 0) return new Token(keyword.Item2, id);
                }

                return id;
            }

            if (ich == '"' || ich == '\'')
            {
                int startedWith = ich;
                int number = 1;

                ich = reader.Peek(number);
                while (ich == startedWith && reader.Peek(number + 1) == startedWith)
                {
                    number += 2;
                    ich = reader.Peek(number);
                }
                while (ich >= 0 && (ich != startedWith || reader.Peek(number + 1) == startedWith))
                {
                    if (ich == '\\')
                    {
                        number = CountEscapeSeq(number);
                    }
                    else
                    {
                        if (ich == '\r' || ich == '\n')
                        {
                            throw new LexerException(this, "Unexpected end of line during string.");
                        }
                        number++;
                    }

                    ich = reader.Peek(number);
                    while (ich == startedWith && reader.Peek(number + 1) == startedWith)
                    {
                        number += 2;
                        ich = reader.Peek(number);
                    }
                }

                if (ich != startedWith)
                {
                    throw new LexerException(this, $"Expected '{startedWith}', found {DebugChar(ich)}");
                }

                number++;

                return new Token(Tokens.String, reader.GetParsingContext(), reader.Read(number), reader.GetParsingContext());
            }

            if (char.IsDigit((char)ich) || ich == '.')
            {
                // numbers
                int number = 0;
                bool fractional = false;

                if (char.IsDigit((char)ich))
                {
                    if (reader.Peek(0) == '0' && reader.Peek(1) == 'x')
                    {
                        number = 2;

                        ich = reader.Peek(number);
                        while (char.IsDigit((char)ich) || (ich >= 'a' && ich <= 'f') || (ich >= 'A' && ich <= 'F'))
                        {
                            number++;

                            ich = reader.Peek(number);
                        }

                        return new Token(Tokens.HexInt, reader.GetParsingContext(), reader.Read(number), reader.GetParsingContext());
                    }

                    number = 1;
                    ich = reader.Peek(number);
                    while (char.IsDigit((char)ich))
                    {
                        number++;

                        ich = reader.Peek(number);
                    }
                }

                if (ich == '.')
                {
                    fractional = true;

                    // Skip the '.'
                    number++;

                    ich = reader.Peek(number);
                    if (!char.IsDigit((char)ich))
                        throw new LexerException(this, $"Expected DIGIT, found {(char)ich}");

                    while (char.IsDigit((char)ich))
                    {
                        number++;

                        ich = reader.Peek(number);
                    }
                }

                if (ich == 'e' || ich == 'E')
                {
                    fractional = true;

                    // letter
                    number++;

                    ich = reader.Peek(number);
                    if (ich == '+' || ich == '-')
                    {
                        number++;

                        ich = reader.Peek(number);
                    }

                    if (!char.IsDigit((char)ich))
                        throw new LexerException(this, $"Expected DIGIT, found {(char)ich}");

                    while (char.IsDigit((char)ich))
                    {
                        number++;

                        ich = reader.Peek(number);
                    }
                }

                if (fractional)
                    return new Token(Tokens.Double, reader.GetParsingContext(), reader.Read(number), reader.GetParsingContext());

                return new Token(Tokens.Integer, reader.GetParsingContext(), reader.Read(number), reader.GetParsingContext());
            }

            throw new LexerException(this, $"Unexpected character: {reader.Peek()}");
        }

        private static string DebugChar(int ich)
        {
            if (ich < 0)
                return "EOF";

            switch (ich)
            {
            case 0: return "'\\0'";
            case 8: return "'\\b'";
            case 9: return "'\\t'";
            case 10: return "'\\n'";
            case 13: return "'\\r'";
            default:
                return char.IsControl((char)ich) ? $"'\\u{ich:X4}'" : $"'{(char)ich}'";
            }
        }

        private int CountEscapeSeq(int number)
        {
            int ich = reader.Peek(number);
            if (ich != '\\')
                throw new LexerException(this, "Internal Error");

            number++;

            ich = reader.Peek(number);
            switch (ich)
            {
            case '\n':
                return ++number;
            case '\r':
                if (reader.Peek(number + 1) == '\n')
                    return number += 2;
                return ++number;
            case '0':
            case 'b':
            case 'f':
            case 'n':
            case 'r':
            case 't':
            case '"':
            case '\'':
            case '\\':
                return ++number;
            }

            if (ich == 'x' || ich == 'u')
            {
                number++;

                ich = reader.Peek(number);
                if (char.IsDigit((char)ich) || (ich >= 'a' && ich <= 'f') || (ich >= 'A' && ich <= 'F'))
                {
                    number++;

                    ich = reader.Peek(number);
                    if (char.IsDigit((char)ich) || (ich >= 'a' && ich <= 'f') || (ich >= 'A' && ich <= 'F'))
                    {
                        number++;

                        ich = reader.Peek(number);
                        if (char.IsDigit((char)ich) || (ich >= 'a' && ich <= 'f') || (ich >= 'A' && ich <= 'F'))
                        {
                            number++;

                            ich = reader.Peek(number);
                            if (char.IsDigit((char)ich) || (ich >= 'a' && ich <= 'f') || (ich >= 'A' && ich <= 'F'))
                            {
                                number++;
                            }
                        }
                    }
                }
                return number;
            }

            //throw new LexerException(this, $"Unknown escape sequence \\{ich}");

            // assume single character sequence.
            return ++number;
        }

        public override string ToString()
        {
            return $"{{Lexer ahead={string.Join(", ", lookAhead)}, reader={reader}}}";
        }

        public void Dispose()
        {
            reader.Dispose();
        }

        public static bool IsValidIdentifier(string ident)
        {
            bool first = true;

            foreach (char c in ident)
            {
                if (!char.IsLetter(c) && c != '_')
                {
                    if (first || !char.IsDigit(c))
                    {
                        return false;
                    }
                }

                first = false;
            }

            return true;
        }

        public static string UnescapeString(Token t, bool cancelRtl = false)
        {
            StringBuilder sb = new StringBuilder();

            char startQuote = (char)0;

            bool inEscape = false;

            bool inHexEscape = false;
            int escapeAcc = 0;
            int escapeDigits = 0;
            int escapeMax = 0;

            bool seenRtl = false;

            foreach (char c in t.Text)
            {
                if (startQuote != 0)
                {
                    if (inHexEscape)
                    {
                        if (escapeDigits == escapeMax)
                        {
                            sb.Append((char)escapeAcc);
                            inHexEscape = false;
                        }
                        else if (char.IsDigit(c))
                        {
                            escapeAcc = (escapeAcc << 4) + (c - '0');
                        }
                        else if ((escapeDigits < escapeMax) && (c >= 'a') && (c <= 'f'))
                        {
                            escapeAcc = (escapeAcc << 4) + 10 + (c - 'a');
                        }
                        else if ((escapeDigits < escapeMax) && (c >= 'A') && (c <= 'F'))
                        {
                            escapeAcc = (escapeAcc << 4) + 10 + (c - 'A');
                        }
                        else
                        {
                            sb.Append((char)escapeAcc);
                            inHexEscape = false;
                        }
                        escapeDigits++;
                    }

                    if (inEscape)
                    {
                        switch (c)
                        {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\'':
                            sb.Append('\'');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '0':
                            sb.Append('\0');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 'x':
                            inHexEscape = true;
                            escapeAcc = 0;
                            escapeDigits = 0;
                            escapeMax = 2;
                            break;
                        case 'u':
                            inHexEscape = true;
                            escapeAcc = 0;
                            escapeDigits = 0;
                            escapeMax = 4;
                            break;
                        }
                        inEscape = false;
                    }
                    else if (!inHexEscape)
                    {
                        if (c == startQuote)
                        {
                            if (seenRtl && cancelRtl)
                                sb.Append((char)0x200E);
                            return sb.ToString();
                        }
                        switch (c)
                        {
                        case '\\':
                            inEscape = true;
                            break;
                        default:
                            sb.Append(c);
                            break;
                        }
                    }
                }
                else
                {
                    switch (c)
                    {
                    case '"':
                        startQuote = '"';
                        break;
                    case '\'':
                        startQuote = '\'';
                        break;
                    case (char)0x200E:
                        seenRtl = false;
                        sb.Append(c);
                        break;
                    case (char)0x200F:
                        seenRtl = true;
                        sb.Append(c);
                        break;
                    default:
                        sb.Append(c);
                        break;
                    }
                }
            }

            throw new ParserException(t, "Invalid string literal");
        }

        public static string EscapeString(string p)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append('"');
            foreach (char c in p)
            {
                bool printable = (c >= 32 && c < 127)
                                 || char.IsWhiteSpace(c)
                                 || char.IsLetter(c)
                                 || char.IsDigit(c);
                if (!char.IsControl(c) && printable && c != '"' && c != '\\')
                {
                    sb.Append(c);
                    continue;
                }


                sb.Append('\\');
                switch (c)
                {
                case '\b':
                    sb.Append('b');
                    break;
                case '\t':
                    sb.Append('t');
                    break;
                case '\n':
                    sb.Append('n');
                    break;
                case '\f':
                    sb.Append('f');
                    break;
                case '\r':
                    sb.Append('r');
                    break;
                case '\"':
                    sb.Append('\"');
                    break;
                case '\\':
                    sb.Append('\\');
                    break;
                default:
                    sb.Append(c > 0xFF ? $"u{(int)c:X4}" : $"u{(int)c:X2}");
                    break;
                }
            }
            sb.Append('"');

            return sb.ToString();
        }

        public ParsingContext GetParsingContext()
        {
            if (lookAhead.Count > 0)
                return lookAhead[0].Context;
            return reader.GetParsingContext();
        }
    }
}
