using System;
using System.IO;
using System.Text;
using GDDL;
using TransDiffer.Parser.Exceptions;
using TransDiffer.Parser.Util;

namespace TransDiffer.Parser
{
    public class Reader : IContextProvider, IDisposable
    {
        private readonly QueueList<int> unreadBuffer = new QueueList<int>();

        private readonly StreamReader dataSource;
        private readonly string sourceName;

        private bool endQueued;
        private int offset = 0;
        private int line = 1;
        private int column = 1;

        public Reader(string source)
        {
            sourceName = source;
            dataSource = new StreamReader(source);
        }

        private void Require(int number)
        {
            int needed = number - unreadBuffer.Count;
            if (needed > 0)
            {
                NeedChars(needed);
            }
        }

        private void NeedChars(int needed)
        {
            while (needed-- > 0)
            {
                if (endQueued)
                {
                    throw new ReaderException(this, "Tried to Read beyond the end of the file.");
                }

                int ch = dataSource.Read();
                unreadBuffer.Add(ch);
                if (ch < 0)
                    endQueued = true;
            }
        }

        public int Peek()
        {
            return Peek(0);
        }

        public int Peek(int index)
        {
            Require(index + 1);

            return unreadBuffer[index];
        }

        private int Next()
        {
            int ch = unreadBuffer.Remove();
            offset++;
            column++;
            if (ch == '\n')
            {
                column = 1;
                line++;
            }
            else if (ch == '\r')
            {
                Require(1);
                if (Peek() == '\n')
                {
                    // delay processing linebreak
                }
                else
                {
                    column = 1;
                    line++;
                }
            }

            return ch;
        }

        public string Read(int count)
        {
            Require(count);
            var b = new StringBuilder();
            while (count-- > 0)
            {
                int ch = Next();
                if (ch < 0)
                    throw new ReaderException(this, "Tried to Read beyond the end of the file.");
                b.Append((char)ch);
            }
            return b.ToString();
        }

        public void Skip(int count)
        {
            Require(count);
            while (count-- > 0)
                Next();
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            foreach (var ch in unreadBuffer)
            {
                b.Append((char)ch);
            }
            return $"{{Reader ahead={b}}}";
        }

        public void Dispose()
        {
            dataSource.Dispose();
        }

        public ParsingContext GetParsingContext()
        {
            return new ParsingContext(sourceName, offset, line, column);
        }
    }
}
