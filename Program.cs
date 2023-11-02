using System.Text.RegularExpressions;

namespace Mal;

#region MalTypes

internal abstract class MalType
{}

internal abstract class MalAtomic : MalType
{}

internal enum Symbol
{
    RIGHT_PARENTHESIS,
    PLUS_SYMBOL,
    STAR_SYMBOL,
    LEFT_PARENTHESIS
}

internal class MalSymbolType : MalAtomic
{
    internal Symbol Symbol;

    internal MalSymbolType(Symbol symbol)
    {
        Symbol = symbol;
    }
}

internal class MalNumberType : MalAtomic
{
    internal int Number;

    public MalNumberType(int number)
    {
        Number = number;
    }
}

internal class MalListType : MalType
{
    internal List<MalType> MalTypes;
    
    public MalListType(List<MalType> malTypes)
    {
        MalTypes = malTypes;
    }
}

#endregion

#region Reader

internal class Token
{
    internal (int, int) Position = (-1, -1);
    internal string Text = "";
}

internal class Reader
{
    private int _position = 0;

    private readonly Regex _malRegex =
        new(@"[\s,]*(~@|[\[\]{}()'`~^@]|""(?:\\.|[^\\""])*""?|;.*|[^\s\[\]{}('""`,;)]*)");

    private readonly List<Token> _tokens;

    public Reader(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public Reader()
    {
    }

    private Token Peek()
    {
        return _tokens[_position];
    }

    private Token Next()
    {
        return _tokens[_position++];
    }

    internal MalType ReadString(string text)
    {
        var tokens = Tokenize(text);
        var reader = new Reader(tokens);
        var malType = reader.ReadForm();

        return malType;
    }

    private MalListType ReadList()
    {
        Next();
        
        var list = new List<MalType>(){new MalSymbolType(Symbol.LEFT_PARENTHESIS)};

        var foundEnd = false;
        while (!foundEnd)
        {
            var token = ReadForm();
            
            if (token is MalSymbolType symbol)
            {
                switch (symbol.Symbol)
                {
                   case Symbol.RIGHT_PARENTHESIS:
                       foundEnd = true;
                       break;
                }
            }
            
            list.Add(token);

            if (foundEnd) break;
            
            Next();
        }
        
        return new MalListType(list);
    }

    private MalNumberType ReadNumber()
    {
        var token = Peek();

        var isNumber = int.TryParse(token.Text, out var number);

        if (!isNumber)
        {
            throw new Exception($"Expected a number but found {token.Text}");
        }
        
        return new MalNumberType(number);
    }

    private MalAtomic ReadAtomic()
    {
        var token = Peek();

        MalAtomic malAtomic = token.Text[0] switch
        {
            ')' => new MalSymbolType(Symbol.RIGHT_PARENTHESIS),
            '+' => new MalSymbolType(Symbol.PLUS_SYMBOL),
            '*' => new MalSymbolType(Symbol.STAR_SYMBOL),
            >= '0' and <= '9' => ReadNumber(),
            _ => throw new Exception($"Bad atomic {token.Text}")
        };

        return malAtomic;
    }

    private MalType ReadForm()
    {
        var token = Peek();

        MalType result = token.Text switch
        {
            "(" => ReadList(),
            _ => ReadAtomic()
        };

        return result;
    }

    private List<Token> Tokenize(string text)
    { 
        var tokens = new List<Token>();

        foreach (Match match in _malRegex.Matches(text))
        {
            var token = new Token()
            {
                Text = match.Value.Trim(),
                Position = (match.Index, match.Index + match.Value.Trim().Length),
            };
            
            tokens.Add(token);
        }

        return tokens;
    }
}

#endregion

#region Printer

internal class Printer
{
    internal string PrintString(MalType malType)
    {
        if (malType is MalSymbolType symbol)
        {
            var text = symbol.Symbol switch
            {
                Symbol.PLUS_SYMBOL => "+",
                Symbol.STAR_SYMBOL => "*",
                Symbol.LEFT_PARENTHESIS => "(",
                Symbol.RIGHT_PARENTHESIS => ")",
                _ => throw new NotImplementedException()
            };

            return text;
        }

        if (malType is MalListType malListType)
        {
            List<string> results = new List<string>();
            foreach (var type in malListType.MalTypes)
            {
                var result = PrintString(type);
                results.Add(result);
            }
            return string.Join(" ", results.ToArray());
        }

        if (malType is MalNumberType malNumberType)
        {
            return malNumberType.Number.ToString();
        }

        throw new NotImplementedException($"String type not implemented for {malType.ToString()}");
    }
}

#endregion

internal static class Program
{
    static MalType Read()
    {
        Console.Write("> ");
        var input = Console.ReadLine() ?? "";
        var reader = new Reader();

        var response = reader.ReadString(input);
        
        return response;
    }

    static MalType Evaluate(MalType malType)
    {
        return malType;
    }

    static void Print(MalType malType)
    {
        var printer = new Printer();
        Console.WriteLine(printer.PrintString(malType));
    }

    static void ReadEvaluatePrint()
    {
        Print(Evaluate(Read()));
    }
    
    public static void Main(string[] args)
    {
        while (true)
        {
            ReadEvaluatePrint();
        }
    }
}