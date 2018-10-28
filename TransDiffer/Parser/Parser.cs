using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            if (IsExpressionStartToken())
            {
                var ident = Expression();
                if (Lexer.Peek() == Tokens.Menu) return MenuStatement(ident);
                if (Lexer.Peek() == Tokens.MenuEx) return MenuStatement(ident);
                if (Lexer.Peek() == Tokens.Dialog) return DialogStatement(ident,false);
                if (Lexer.Peek() == Tokens.DialogEx) return DialogStatement(ident,true);
                //if (Lexer.Peek() == Tokens.Begin) return OrphanedBeginEndBlock();
            }
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

        private MenuDefinition MenuStatement(ExpressionValue ident)
        {
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
            if (IsExpressionStartToken())
            {
                ident = Expression(false);
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);

                // Ignore all remaining expressions
                while (IsExpressionStartToken())
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
                EntryType = ctx,
                Identifier = ident,
                TextValue = label
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

        static int Solve(List<Token> tokens)
        {
            Debug.Assert(tokens.All(tok => tok.Name == Tokens.Minus || tok.Name == Tokens.Integer));
            int value = 0;
            bool bNeg = false;
            foreach(var tok in tokens)
            {
                if (tok.Name == Tokens.Minus)
                {
                    Debug.Assert(bNeg == false);
                    bNeg = true;
                }
                else
                {
                    int val;
                    Debug.Assert(tok.Name == Tokens.Integer);
                    bool bParse = int.TryParse(tok.Text, out val);
                    Debug.Assert(bParse);
                    if (bNeg)
                    {
                        bNeg = false;
                        val *= -1;
                    }
                    value += val;
                }
            }
            Debug.Assert(bNeg == false);
            return value;
        }

        private System.Windows.Rect Dimensions()
        {
            int[] DlgPos = new int[4];
            int cur = 0;
            while (IsExpressionStartToken())
            {
                var expr = Expression();
                DlgPos[cur] = Solve(expr.Tokens);

                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);

                if (++cur == DlgPos.Length)
                {
                    return new System.Windows.Rect(DlgPos[0], DlgPos[1], DlgPos[2], DlgPos[3]);
                }
            }
            Debug.Assert(false);
            return new System.Windows.Rect();
        }

        private ExpressionValue StyleStatement()
        {
            var expr = Expression();
            Debug.Assert(expr.Tokens.All(
                tok => tok.Name == Tokens.Pipe ||
                tok.Name == Tokens.Not ||
                tok.Name == Tokens.Ident ||
                tok.Name == Tokens.Integer ||
                tok.Name == Tokens.HexInt));
            return expr;
        }

        private DialogDefinition DialogStatement(ExpressionValue ident, bool isEx)
        {
            var ctx = PopExpected(isEx ? Tokens.DialogEx : Tokens.Dialog);

            if (Lexer.Peek() == Tokens.Discardable)
                PopExpected(Tokens.Discardable);

            var de = new DialogDefinition();
            de.Identifier = ident;
            de.Context = ctx.Context;
            de.EntryType = ctx;

            de.Dimensions = Dimensions();

            while (IsExpressionStartToken())
            {
                Expression();

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
                        de.Style = StyleStatement();
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.ExStyle:
                    {
                        PopExpected(Tokens.ExStyle);
                        de.ExStyle = StyleStatement();
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                    }
                    break;
                case Tokens.Font:
                    {
                        PopExpected(Tokens.Font);
                        var size = PopExpected(Tokens.Double, Tokens.Integer);
                        double sizeVal;
                        double.TryParse(size.Text, out sizeVal);
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);
                        var name = PopExpected(Tokens.String);
                        if (Lexer.Peek() == Tokens.Comma)
                            PopExpected(Tokens.Comma);

                        de.Font = new Font() { Name = name.Text, Size = (float)sizeVal };
                    }
                    break;
                case Tokens.Caption:
                    {
                        PopExpected(Tokens.Caption);
                        var caption = PopExpected(Tokens.String);
                        de.TextValue = caption;
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
                while (IsExpressionStartToken())
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
            System.Windows.Rect Bounds = new System.Windows.Rect();
            string ctrlType = null;
            ExpressionValue Style = null;

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
                if (ctx.Name == Tokens.Control)
                {
                    var ctrlTypeToken = PopExpected(Tokens.String, Tokens.Ident);
                    if (ctrlTypeToken.Name == Tokens.String)
                        ctrlType = ctrlTypeToken.Text;
                    else
                        ctrlType = ClassNames.Translate(ctrlTypeToken.Text);
                    PopExpected(Tokens.Comma);
                    Style = StyleStatement();
                    PopExpected(Tokens.Comma);
                }
                break;
            case Tokens.Icon:
            case Tokens.ListBox:
            case Tokens.EditText:
            case Tokens.ComboBox:
                ctx = Lexer.Pop();
                ident = Expression();
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
                if (ctx.Name == Tokens.Icon)
                {
                    value = ident.Tokens.First();
                    ident = Expression();
                    if (Lexer.Peek() == Tokens.Comma)
                        PopExpected(Tokens.Comma);
                }
                break;
            default:
                throw new ParserException(this, $"Internal Error: Unexpected Look-Ahead {Lexer.Peek(0)}");
            }

            Bounds = Dimensions();
            if (Style == null && IsExpressionStartToken())
            {
                Style = StyleStatement();
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
            }

            // Ignore all remaining expressions
            while (IsExpressionStartToken())
            {
                var expr = Expression();
                // ignore, we don't need it
                if (Lexer.Peek() == Tokens.Comma)
                    PopExpected(Tokens.Comma);
            }


            var ste = new DialogControl
            {
                Context = ctx.Context,
                EntryType = ctx,
                Identifier = ident,
                TextValue = value,
                Dimensions = Bounds,
                GenericControlType = ctrlType ?? string.Empty,
                Style = Style
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
                Identifier = ident,
                TextValue = text
            };

            return ste;
        }

        private bool IsExpressionStartToken(int offset = 0, bool allowStrings = true)
        {
            return
                (allowStrings ? IsExpressionValue(offset) : IsExpressionNumber(offset)) ||
                IsExpressionUnaryOperator(offset) ||
                Lexer.Peek(offset) == Tokens.LParen;
        }

        private bool IsExpressionToken(int offset = 0, bool allowStrings = true)
        {
            return
                (allowStrings ? IsExpressionValue(offset) : IsExpressionNumber(offset)) ||
                IsExpressionBinaryOperator(offset) ||
                IsExpressionUnaryOperator(offset) ||
                Lexer.Peek(offset) == Tokens.LParen ||
                Lexer.Peek(offset) == Tokens.RParen;
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

        private bool IsExpressionBinaryOperator(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Plus ||
                Lexer.Peek(offset) == Tokens.Minus ||
                Lexer.Peek(offset) == Tokens.Asterisk ||
                Lexer.Peek(offset) == Tokens.Slash ||
                Lexer.Peek(offset) == Tokens.Pipe ||
                Lexer.Peek(offset) == Tokens.Ampersand ||
                Lexer.Peek(offset) == Tokens.And ||
                Lexer.Peek(offset) == Tokens.Or;
        }

        private bool IsExpressionUnaryOperator(int offset = 0)
        {
            return
                Lexer.Peek(offset) == Tokens.Minus ||
                Lexer.Peek(offset) == Tokens.Squiggly ||
                Lexer.Peek(offset) == Tokens.Not;
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
            if (allowStrings ? IsExpressionValue() : IsExpressionNumber())
            {
                // Expression ::= Value (Operator Expression)?

                value.Tokens.Add(Lexer.Pop());

                if (IsExpressionBinaryOperator())
                {
                    value.Tokens.Add(Lexer.Pop());
                    RecurseExpression(value, allowStrings);
                }
            }
            else if (IsExpressionUnaryOperator() && IsExpressionStartToken(1, allowStrings))
            {
                // Expression ::= Operator Expression

                value.Tokens.Add(Lexer.Pop());
                RecurseExpression(value, allowStrings);
            }
            else if(Lexer.Peek() == Tokens.LParen)
            {
                // Expression ::= '(' Expression ')' (Operator Expression)?

                value.Tokens.Add(PopExpected(Tokens.LParen));
                RecurseExpression(value, allowStrings);
                value.Tokens.Add(PopExpected(Tokens.RParen));

                if (IsExpressionBinaryOperator())
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
