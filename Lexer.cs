/*
    (C) Neil Mitchell 2012
    This module is licensed under the BSD license
*/

namespace VisualStudio.Haskell
{
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

static class Lexer
{
    public enum LineState
    {
        Normal,
        MultilineComment1, // 1 nesting depth
        MultilineComment2, // 2 nesting depth
        MultilineComment3,
        // more, but just index by integer
    }

    public enum Token
    {
        OpenParen,
        CloseParen,
        OpenSquare,
        CloseSquare,
        OpenCurly,
        CloseCurly,

        Unknown = 100,
        String,
        Char,
        Comment,
        Keyword,
    }

    public struct Lexeme
    {
        public int Start;
        public int Length;
        public Token Token;
    }

    public static bool IsBracket(Token t){return (int) t < 100;}

    public static bool IsLeftBracket(Token t){return (int) t < 100 && (int) t % 2 == 0;}
    public static bool IsRightBracket(Token t){return (int) t < 100 && (int) t % 2 == 1;}

    public static Token AlternateBracket(Token t)
    {
        int i = (int) t;
        if (i >= 100) return t;
        return (Token) (i % 2 == 0 ? i + 1 : i - 1);
    }

    public static bool IsComment(Token t)
    {
        return t == Token.Comment;
    }

    public static bool IsString(Token t)
    {
        return t == Token.String || t == Token.Char;
    }

    public static bool IsKeyword(Token t)
    {
        return t == Token.Keyword;
    }

    private const string AscSymbols = "!#$%&*+./<=>?@\\^|-~:";
    private static bool IsWhite(char c){return char.IsWhiteSpace(c);}
    private static bool IsSymbol(char c){return char.IsSymbol(c) || AscSymbols.IndexOf(c) != -1;}
    private static bool IsIdentStart(char c){return char.IsLetter(c) || c == '_';}
    private static bool IsIdentCont(char c){return char.IsLetterOrDigit(c) || c == '_' || c == '\'';}

    private static HashSet<string> KeywordIds = new HashSet<string>(new string[]
        {"case","class","data","deriving","do","else","forall","if","import","in","infix"
        ,"infixl","infixr","instance","let","module","newtype","of","then","type","where"});
    private static HashSet<string> KeywordImport = new HashSet<string>(new string[]
        {"hiding","qualified","as"});
    private static HashSet<string> KeywordSyms = new HashSet<string>(new string[]
        {"->","<-","::","@","=","|","\\"});

    private static List<string> SplitText(string s)
    {
        // split the text up, into words, using the rough same algorithm as VS
        // divide into 3 classes, space, alphanum_, other
        // take consecutive spans of each one
        // Used for inside comments and strings
        Func<char, int> Classify = c =>
            char.IsWhiteSpace(c) ? 1 :
            char.IsLetterOrDigit(c) || c == '_' ? 2 :
            3;

        var res = new List<string>();
        if (s.Length == 0) return res;

        int start = 0;
        int last = Classify(s[0]);
        for (int i = 1; i < s.Length; i++)
        {
            var now = Classify(s[i]);
            if (now == last) continue;
            res.Add(s.Substring(start, i - start));
            start = i;
            last = now;
        }
        res.Add(s.Substring(start));
        return res;
    }

    private static void MultilineComment(string s, ref int Index, ref int Depth)
    {
        for (int i = Index; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '{' && i + 1 < s.Length && s[i+1] == '-')
                Depth++;
            else if (c == '}' && i > Index && s[i-1] == '-')
            {
                Depth--;
                if (Depth == 0)
                {
                    Index = i+1;
                    return;
                }
            }
        }
        Index = s.Length;
    }

    public static Lexeme[] Forward(string Text, ref LineState state)
    {
        var res = new List<Lexeme>();

        var i = 0;
        var isImport = false; // True if import keywords are allowed
        if (state >= LineState.MultilineComment1)
        {
            int Depth = (int) state - (int) LineState.MultilineComment1 + 1;
            MultilineComment(Text, ref i, ref Depth);
            int j = 0;
            foreach (var s in SplitText(Text.Substring(0, i)))
            {
                var r = new Lexeme();
                r.Start = j;
                r.Length = s.Length;
                j += s.Length;
                r.Token = Token.Comment;
                if (!char.IsWhiteSpace(s[0]))
                    res.Add(r);
            }
            if (Depth != 0)
            {
                state = (LineState) ((int) LineState.MultilineComment1 + Depth - 1);
                return res.ToArray();
            }
            state = LineState.Normal;
        }

        for (; i < Text.Length; )
        {
            var c = Text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            var r = new Lexeme();
            r.Start = i;
            r.Length = 1;
            r.Token = Token.Unknown;

            isImport = isImport && (c != '(') && (c != ';');
            if (IsSymbol(c))
            {
                int n = 1;
                while (i + n < Text.Length && IsSymbol(Text[i + n]))
                    n++;
                string sym = Text.Substring(i, n);
                if (!(sym.Length >= 2 && sym.Replace("-","").Length == 0))
                {
                    r.Token = KeywordSyms.Contains(sym) ? Token.Keyword : Token.Unknown;
                    r.Length = n;
                }
                else
                {
                    foreach (var part in SplitText(Text.Substring(i)))
                    {
                        r = new Lexeme();
                        r.Start = i;
                        r.Length = part.Length;
                        r.Token = Token.Comment;
                        i += part.Length;
                        if (!char.IsWhiteSpace(part[0]))
                            res.Add(r);
                    }
                    break;
                }
            }
            else if (IsIdentStart(c))
            {
                int n = 1;
                while (i + n < Text.Length && IsIdentCont(Text[i + n]))
                    n++;
                string ident = Text.Substring(i, n);
                r.Token = KeywordIds.Contains(ident) ? Token.Keyword : Token.Unknown;
                if (isImport && KeywordImport.Contains(ident)) r.Token = Token.Keyword;
                if (ident == "import") isImport = true;
                r.Length = n;
            }
            else if (c == '\"' || c == '\'')
            {
                int n = 1;
                while (i + n < Text.Length)
                {
                    if (Text[i+n] == c) {n++; break;}
                    if (Text[i+n] == '\\') n++;
                    n++;
                }
                n = Math.Min(Text.Length - i, n); // in case a trailing \ character
                foreach (var part in SplitText(Text.Substring(i, n)))
                {
                    r = new Lexeme();
                    r.Start = i;
                    r.Length = part.Length;
                    r.Token = c == '\"' ? Token.String : Token.Char;
                    i += part.Length;
                    if (!char.IsWhiteSpace(part[0]))
                        res.Add(r);
                }
                continue;
            }
            else if (c == '[') r.Token = Token.OpenSquare;
            else if (c == ']') r.Token = Token.CloseSquare;
            else if (c == '(') r.Token = Token.OpenParen;
            else if (c == ')') r.Token = Token.CloseParen;
            else if (c == '}') r.Token = Token.CloseCurly;
            else if (c == '{' && Text.Length > i+1 && Text[i+1] == '-')
            {
                int Depth = 0;
                int j = i;
                MultilineComment(Text, ref i, ref Depth);
                foreach (var s in SplitText(Text.Substring(j, i-j)))
                {
                    var rr = new Lexeme();
                    rr.Start = j;
                    rr.Length = s.Length;
                    j += s.Length;
                    rr.Token = Token.Comment;
                    if (!char.IsWhiteSpace(s[0]))
                        res.Add(rr);
                }
                if (Depth != 0)
                {
                    state = (LineState) ((int) LineState.MultilineComment1 + Depth - 1);
                    return res.ToArray();
                }
                continue;
            }
            else if (c == '{') r.Token = Token.OpenCurly;

            res.Add(r);
            i += Math.Max(1, r.Length);
        }
        return res.ToArray();
    }

    private enum TestResult {Norm, Key, Str, Com}

    private static void Test(string command, LineState start, LineState end)
    {
        // first step, parse command
        var cmds = command.Replace('|','\0').Replace("\0\0","|").Split('\0');
        var enms = Enum.GetNames(typeof(TestResult));
        var str = string.Join("", cmds.Where(s => !enms.Contains(s)).ToArray());
        var st = start;
        var res = Forward(str, ref st);
        Assert(st == end);
        var i = 0;
        var expect = TestResult.Norm;
        foreach (var c in cmds)
        {
            if (enms.Contains(c))
                expect = (TestResult) Enum.Parse(typeof(TestResult), c);
            else
            {
                var r = res[i++];
                var y1 = str.Substring(r.Start, r.Length);
                var y2 = c.Trim();
                Assert(str.Substring(r.Start, r.Length) == c.Trim());
                Assert((expect == TestResult.Key) == IsKeyword(r.Token));
                Assert((expect == TestResult.Str) == IsString(r.Token));
                Assert((expect == TestResult.Com) == IsComment(r.Token));
            }
        }
        Assert(i == res.Length);
    }

    private static void Assert(bool b)
    {
        if (!b) throw new Exception();
    }

    public static void Test(string command)
    {
        Test(command, LineState.Normal, LineState.Normal);
    }

    public static void Test()
    {
        Console.WriteLine("Starting tests");
        Test("Key|module |Norm|(|A|,|B|(|..|) |,|C|) |Key|where");
        Test("test |Key|= |Str|\"|more|\"");
        Test("test |Key|= |Str|\"|mor| stuff|\"");
        Test("test |Key|= |Str|\"|mor");
        Test("test |Key|= |Str|\'|mor| stuff|\' |Key|where");
        Test("test |Key|= |Str|\"|mor| \\\"|stuff|\"");
        Test("test |Key|= |Str|\"|mor| \\");
        Test("hello |Com|-- |foo |bar");
        Test("hello |Com|---------------------------------------");
        Test("Key|\\|Norm|x |Key|-> |Norm|x |\\\\ |y");
        Test("hello |Com|{------}|Norm|x");
        Test("hello |Com|{-- |test", LineState.Normal, LineState.MultilineComment1);
        Test("hello |Com|{-- |test |{-", LineState.Normal, LineState.MultilineComment2);
        Test("Com|more |-}|Norm|test", LineState.MultilineComment1, LineState.Normal);
        Test("Com|more |-}|test", LineState.MultilineComment2, LineState.MultilineComment1);
        Test("more|-|}");
        Test("{|x|-|}");
        Test("Com|extra |stuff |goes |here|--", LineState.MultilineComment1, LineState.MultilineComment1);
        Test("qualified |as |Key|= |Norm|as");
        Test("Key|import |qualified |Norm|Prelude|.|Bar |Key|as |Norm|Foo");
        Test("Key|import |qualified |Norm|Prelude|.|Bar |Key|hiding |Norm|(|as|)");
        Console.WriteLine("Tests successful, enter to exit...");
        Console.ReadLine();
    }
}

}
