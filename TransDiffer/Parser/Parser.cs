using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using GDDL;
using TransDiffer.Parser.Exceptions;
using TransDiffer.Parser.Structure;

namespace TransDiffer.Parser
{
    public class Parser : IContextProvider, IDisposable
    {
        int prefixPos = -1;
        readonly Stack<int> prefixStack = new Stack<int>();

        public Lexer Lexer { get; }

        Parser(Lexer lexer)
        {
            Lexer = lexer;
        }

        public static Parser FromFile(string filename)
        {
            return new Parser(new Lexer(new Reader(filename)));
        }

        public ResourceScript Parse()
        {
            return Root();
        }

        private Token PopExpected(params Tokens[] expected)
        {
            Tokens current = Lexer.Peek();
            if (expected.Any(expectedToken => current == expectedToken))
            {
                return Lexer.Pop();
            }

            if (expected.Length != 1)
                throw new ParserException(this,
                    $"Unexpected token {current}. Expected one of: {string.Join(", ", expected)}.");

            throw new ParserException(this, $"Unexpected token {current}. Expected: {expected[0]}.");
        }

        public void BeginPrefixScan()
        {
            prefixStack.Push(prefixPos);
        }

        public Tokens NextPrefix()
        {
            return Lexer.Peek(++prefixPos);
        }

        public void EndPrefixScan()
        {
            prefixPos = prefixStack.Pop();
        }

        private bool HasAny(params Tokens[] tokens)
        {
            var prefix = NextPrefix();
            return tokens.Any(t => prefix == t);
        }

#if false
        private bool prefix_element()
        {
            return prefix_basicElement() || prefix_namedElement();
        }

        private bool prefix_basicElement()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.Nil, Tokens.Null, Tokens.True, Tokens.False,
                Tokens.HexInt, Tokens.Integer, Tokens.Double, Tokens.String);
            EndPrefixScan();

            return r || prefix_backreference() || prefix_set() || prefix_typedSet();
        }

        private bool prefix_namedElement()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.Ident, Tokens.String) && HasAny(Tokens.EqualSign);
            EndPrefixScan();
            return r;
        }

        private bool prefix_backreference()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.Colon) && HasAny(Tokens.Ident);
            EndPrefixScan();

            return r || prefix_identifier();
        }

        private bool prefix_set()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.LBrace);
            EndPrefixScan();
            return r;
        }

        private bool prefix_typedSet()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.Ident) && HasAny(Tokens.LBrace);
            EndPrefixScan();
            return r;
        }

        private bool prefix_identifier()
        {
            BeginPrefixScan();
            var r = HasAny(Tokens.Ident);
            EndPrefixScan();
            return r;
        }
#endif

        private ResourceScript Root()
        {
            var rc = new ResourceScript();
            while (Lexer.Peek() != Tokens.End)
            {
                var e = Element();
                rc.Definition.Add(e);
            }
            PopExpected(Tokens.End);
            return rc;
        }

        private ResourceStatement Element()
        {
            if (Lexer.Peek() == Tokens.Language) return LanguageStatement();
            if (Lexer.Peek() == Tokens.StringTable) return StringTableStatement();
            if (Lexer.Peek() == Tokens.Ident && Lexer.Peek(1) == Tokens.Menu) return MenuStatement();
            if (Lexer.Peek() == Tokens.Ident && Lexer.Peek(1) == Tokens.MenuEx) return MenuStatement();
            if (Lexer.Peek() == Tokens.Ident && Lexer.Peek(1) == Tokens.Dialog) return DialogStatement(false);
            if (Lexer.Peek() == Tokens.Ident && Lexer.Peek(1) == Tokens.DialogEx) return DialogStatement(true);
            //if (Lexer.Peek() == Tokens.Begin) return OrphanedBeginEndBlock();

            //throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
            return new ParseErrorRecovery(Lexer.Pop());
        }

        private LanguageStatement LanguageStatement()
        {
            Token sublang = null;
            var ctx = PopExpected(Tokens.Language);
            var lang = PopExpected(Tokens.Ident);
            if (Lexer.Peek() == Tokens.Comma)
            {
                PopExpected(Tokens.Comma);
                sublang = PopExpected(Tokens.Ident);
            }

            var ls = new LanguageStatement
            {
                Context = ctx.Context,
                Lang = lang,
                SubLang = sublang
            };

            return ls;
        }

        private MenuDefinition MenuStatement()
        {
            var ident = PopExpected(Tokens.Ident);
            var ctx = PopExpected(Tokens.Menu, Tokens.MenuEx);

            if (Lexer.Peek() == Tokens.Discardable)
                PopExpected(Tokens.Discardable);

            PopExpected(Tokens.LBrace, Tokens.Begin);

            var st = new MenuDefinition();
            st.Context = ctx.Context;
            st.Identifier = ident;

            while (Lexer.Peek() != Tokens.RBrace && Lexer.Peek() != Tokens.IdEnd)
            {
                var ste = MenuItemStatement();
                if (ste != null)
                    st.Entries.Add(ste);
            }

            PopExpected(Tokens.RBrace, Tokens.IdEnd);
            return st;
        }

        private MenuItemDefinition MenuItemStatement()
        {
            var ctx = PopExpected(Tokens.MenuItem, Tokens.Popup);
            if (Lexer.Peek() == Tokens.Separator)
            {
                PopExpected(Tokens.Separator);
                return null;
            }

            ExpressionValue ident = null;
            var label = PopExpected(Tokens.String);

            if (Lexer.Peek() == Tokens.Comma)
                PopExpected(Tokens.Comma);
            if (IsExpressionToken())
            {
                ident = Expression(false);
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);

                // Ignore all remaining expressions
                while (IsExpressionToken())
                {
                    var expr = Expression();
                    // ignore, we don't need it
                    if (Lexer.Peek() == Tokens.Comma)
                        PopExpected(Tokens.Comma);
                }

            }

            var mid = new MenuItemDefinition()
            {
                Context = ctx.Context,
                IdentifierToken = ident,
                ValueToken = label
            };

            if (ctx.Name == Tokens.Popup)
            {
                PopExpected(Tokens.LBrace, Tokens.Begin);
                
                while (Lexer.Peek() != Tokens.RBrace && Lexer.Peek() != Tokens.IdEnd)
                {
                    var ste = MenuItemStatement();
                    if (ste != null)
                        mid.Entries.Add(ste);
                }

                PopExpected(Tokens.RBrace, Tokens.IdEnd);
            }

            return mid;
        }


        private DialogDefinition DialogStatement(bool isEx)
        {
            var ident = PopExpected(Tokens.Ident);
            var ctx = PopExpected(isEx ? Tokens.DialogEx : Tokens.Dialog);

            if (Lexer.Peek() == Tokens.Discardable)
                PopExpected(Tokens.Discardable);

            var de = new DialogDefinition();
            de.Identifier = ident;
            de.Context = ctx.Context;

            while (IsExpressionToken())
            {
                var expr = Expression();
                // ignore, we don't need it
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
            }

            while (Lexer.Peek() != Tokens.LBrace && Lexer.Peek() != Tokens.Begin)
            {
                switch (Lexer.Peek())
                {
                case Tokens.Style:
                    {
                        PopExpected(Tokens.Style);
                        Expression();
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.Font:
                    {
                        PopExpected(Tokens.Font);
                        Expression();
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                        PopExpected(Tokens.String);
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.Caption:
                    {
                        PopExpected(Tokens.Caption);
                        var caption = PopExpected(Tokens.String);
                        de.Caption = caption;
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.Menu:
                    {
                        PopExpected(Tokens.Menu);
                        Expression();
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.LBrace:
                case Tokens.Begin:
                    break;
                default:
                    throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
                }
                // Ignore all remaining expressions
                while (IsExpressionToken())
                {
                    var expr = Expression();
                    // ignore, we don't need it
                    if (Lexer.Peek() == Tokens.Comma)
                        PopExpected(Tokens.Comma);
                }
            }
            PopExpected(Tokens.LBrace, Tokens.Begin);

            while (Lexer.Peek() != Tokens.RBrace && Lexer.Peek() != Tokens.IdEnd)
            {
                var dc = DialogControlEntry();
                if (dc != null)
                    de.Entries.Add(dc);
            }

            PopExpected(Tokens.RBrace, Tokens.IdEnd);
            return de;
        }

        private DialogControl DialogControlEntry()
        {
            ExpressionValue ident;
            Token value = null;
            Token ctx;
            switch (Lexer.Peek())
            {
            case Tokens.PushButton:
            case Tokens.DefPushButton:
            case Tokens.GroupBox:
            case Tokens.Control:
            case Tokens.LText:
            case Tokens.RText:
            case Tokens.CText:
            case Tokens.CheckBox:
            case Tokens.AutoCheckBox:
            case Tokens.RadioButton:
            case Tokens.AutoRadioButton:
                ctx = Lexer.Pop();
                if (Lexer.Peek() == Tokens.String)
                {
                    value = PopExpected(Tokens.String);
                }
                else
                {
                    // resource string id
                    Expression();
                }
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
                ident = Expression();
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
                break;
            case Tokens.Icon:
            case Tokens.ListBox:
            case Tokens.EditText:
            case Tokens.ComboBox:
                ctx = Lexer.Pop();
                ident = Expression();
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
                break;
            default:
                throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
            }
            // Ignore all remaining expressions
            while (IsExpressionToken())
            {
                var expr = Expression();
                // ignore, we don't need it
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
            }

            if (value == null)
                return null;

            var ste = new DialogControl
            {
                Context = ctx.Context,
                IdentifierToken = ident,
                ValueToken = value
            };

            return ste;
        }

        private StringTable StringTableStatement()
        {
            var ctx = PopExpected(Tokens.StringTable);
            if (Lexer.Peek() == Tokens.Discardable)
                PopExpected(Tokens.Discardable);
            PopExpected(Tokens.LBrace, Tokens.Begin);

            var st = new StringTable();
            st.Context = ctx.Context;

            while (Lexer.Peek() != Tokens.RBrace && Lexer.Peek() != Tokens.IdEnd)
            {
                var ste = StringTableElement();
                st.Entries.Add(ste);
            }

            PopExpected(Tokens.RBrace, Tokens.IdEnd);
            return st;
        }

        private StringTableEntry StringTableElement()
        {
            var ident = Expression(false);
            if (Lexer.Peek() == Tokens.Comma)
                PopExpected(Tokens.Comma);
            var text = PopExpected(Tokens.String);

            var ste = new StringTableEntry
            {
                Context = ident.Context,
                IdentifierToken = ident,
                ValueToken = text
            };

            return ste;
        }

        private bool IsExpressionToken()
        {
            return
                Lexer.Peek() == Tokens.Integer ||
                Lexer.Peek() == Tokens.HexInt ||
                Lexer.Peek() == Tokens.Double ||
                Lexer.Peek() == Tokens.String ||
                Lexer.Peek() == Tokens.Ident ||
                Lexer.Peek() == Tokens.Plus ||
                Lexer.Peek() == Tokens.Minus ||
                Lexer.Peek() == Tokens.Asterisk ||
                Lexer.Peek() == Tokens.Slash ||
                Lexer.Peek() == Tokens.Pipe ||
                Lexer.Peek() == Tokens.Ampersand ||
                Lexer.Peek() == Tokens.LParen ||
                Lexer.Peek() == Tokens.RParen;
        }

        private bool IsExpressionValue(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Integer ||
                Lexer.Peek(offset) == Tokens.HexInt ||
                Lexer.Peek(offset) == Tokens.Double ||
                Lexer.Peek(offset) == Tokens.String ||
                Lexer.Peek(offset) == Tokens.Ident;
        }

        private bool IsExpressionNumber(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Integer ||
                Lexer.Peek(offset) == Tokens.HexInt ||
                Lexer.Peek(offset) == Tokens.Double ||
                Lexer.Peek(offset) == Tokens.Ident;
        }

        private bool IsExpressionOperator(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Plus ||
                Lexer.Peek(offset) == Tokens.Minus ||
                Lexer.Peek(offset) == Tokens.Asterisk ||
                Lexer.Peek(offset) == Tokens.Slash ||
                Lexer.Peek(offset) == Tokens.Pipe ||
                Lexer.Peek(offset) == Tokens.Ampersand;
        }

        private bool IsExpressionUnaryOperator(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Minus;
        }

        private ExpressionValue Expression(bool allowStrings = true)
        {
            var exp = new ExpressionValue();

            // LAZY PARSING, could re-parse later to decide precedence and such
            RecurseExpression(exp, allowStrings);

            exp.Context = exp.Tokens.First().Context;
            exp.ContextEnd = exp.Tokens.Last().ContextEnd;

            return exp;
        }

        private void RecurseExpression(ExpressionValue value, bool allowStrings)
        {
            // Expression ::= Value
            // Expression ::= Operator Value
            // Expression ::= Value Operator Expression
            // Expression ::= ( Expression )
            if (allowStrings ? IsExpressionValue() : IsExpressionNumber())
            {
                value.Tokens.Add(Lexer.Pop());
                if (IsExpressionOperator())
                {
                    value.Tokens.Add(Lexer.Pop());
                    RecurseExpression(value, allowStrings);
                }
            }
            else if (IsExpressionUnaryOperator() && (allowStrings ? IsExpressionValue(1) : IsExpressionNumber(1)))
            {
                value.Tokens.Add(Lexer.Pop());
                value.Tokens.Add(Lexer.Pop());
            }
            else if(Lexer.Peek() == Tokens.LParen)
            {
                value.Tokens.Add(PopExpected(Tokens.LParen));
                RecurseExpression(value, allowStrings);
                value.Tokens.Add(PopExpected(Tokens.RParen));
                if (IsExpressionOperator())
                {
                    value.Tokens.Add(Lexer.Pop());
                    RecurseExpression(value, allowStrings);
                }
            }
            else
            {
                throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
            }
        }

#if false
        private Element BasicElement()
        {
            if (Lexer.Peek() == Tokens.Ident) return IdentifierValue(PopExpected(Tokens.Ident));
            if (Lexer.Peek() == Tokens.Integer) return IntValue(PopExpected(Tokens.Integer));
            if (Lexer.Peek() == Tokens.HexInt) return IntValue(PopExpected(Tokens.HexInt), 16);
            if (Lexer.Peek() == Tokens.Integer) return IntValue(PopExpected(Tokens.Integer));
            if (Lexer.Peek() == Tokens.Double) return FloatValue(PopExpected(Tokens.Double));
            if (Lexer.Peek() == Tokens.String) return StringValue(PopExpected(Tokens.String));

            throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
        }

        private Element MenuStatement()
        {
            var name = PopExpected(Tokens.Ident, Tokens.String);

            var n = name.Name == Tokens.Ident ? name.Text : Lexer.UnescapeString(name);

            PopExpected(Tokens.EqualSign);

            if (!prefix_basicElement())
                throw new ParserException(this, $"Expected a basic element after EqualSign, found {Lexer.Peek()} instead");

            var b = BasicElement();

            b.Name = n;

            return b;
        }

        private Set Set()
        {
            PopExpected(Tokens.LBrace);

            var s = Structure.Element.Set();

            while (Lexer.Peek() != Tokens.RBrace)
            {
                finishedWithRbrace = false;

                if (!prefix_element())
                    throw new ParserException(this, $"Expected element after LBRACE, found {Lexer.Peek()} instead");

                s.Add(Element());

                if (Lexer.Peek() != Tokens.RBrace)
                {
                    if (!finishedWithRbrace || (Lexer.Peek() == Tokens.Comma))
                    {
                        PopExpected(Tokens.Comma);
                    }
                }
            }

            PopExpected(Tokens.RBrace);

            finishedWithRbrace = true;

            return s;
        }

        private Set TypedSet()
        {
            var type = Identifier();

            if (!prefix_set())
                throw new ParserException(this, "Internal error");
            var s = Set();

            s.TypeName = type;

            return s;
        }
#endif

        private string Identifier()
        {
            if (Lexer.Peek() == Tokens.Ident) return PopExpected(Tokens.Ident).Text;

            throw new ParserException(this, "Internal error");
        }

        public static Value NullValue(Token token)
        {
            return GDDL.Structure.Element.NullValue();
        }

        public static Value IntValue(Token token)
        {
            return GDDL.Structure.Element.IntValue(long.Parse(token.Text, CultureInfo.InvariantCulture));
        }

        public static Value IntValue(Token token, int _base)
        {
            return
                GDDL.Structure.Element.IntValue(long.Parse(token.Text.Substring(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture));
        }

        public static Value FloatValue(Token token)
        {
            return GDDL.Structure.Element.FloatValue(double.Parse(token.Text, CultureInfo.InvariantCulture));
        }

        public static Value StringValue(Token token)
        {
            return GDDL.Structure.Element.StringValue(Lexer.UnescapeString(token));
        }

        public ParsingContext GetParsingContext()
        {
            return Lexer.GetParsingContext();
        }

        public void Dispose()
        {
            Lexer.Dispose();
        }
    }
}
