using System.Reflection;
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
    LEFT_PARENTHESIS,
    MINUS_SYMBOL,
    DIVIDE_SYMBOL
}

internal class MalFunctionType : MalType
{
    internal Func<MalListType, MalType> Function;

    public MalFunctionType(Func<MalListType, MalType> function)
    {
        Function = function;
    }
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
    internal readonly int Number;

    public MalNumberType(int number)
    {
        Number = number;
    }
    
    public static MalNumberType operator +(MalNumberType a, MalNumberType b)
    {
        return new MalNumberType(a.Number + b.Number);
    }
    
    public static MalNumberType operator -(MalNumberType a, MalNumberType b)
    {
        return new MalNumberType(a.Number - b.Number);
    }
    
    public static MalNumberType operator *(MalNumberType a, MalNumberType b)
    {
        return new MalNumberType(a.Number * b.Number);
    }
    
    public static MalNumberType operator /(MalNumberType a, MalNumberType b)
    {
        return new MalNumberType((int)Math.Round((double)a.Number / b.Number));
    }
}

internal class MalListType : MalType
{
    internal List<MalType> MalTypes { get; set; }
    
    
    public MalListType(List<MalType> malTypes)
    {
        MalTypes = malTypes;
    }

    public MalListType Parameters()
    {
        return new MalListType(MalTypes.Skip(1).ToList());
    }

    public object this[int i]
    {
        get => MalTypes[i];
        set => MalTypes[i] = (MalType)value;
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
        
        var list = new List<MalType>(){};

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
            
            if (foundEnd) break;
            
            list.Add(token);
            
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
            '-' => new MalSymbolType(Symbol.MINUS_SYMBOL),
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

internal class Environment
{
    internal Dictionary<Symbol, MalFunctionType> Methods;

    public Environment(Dictionary<Symbol, MalFunctionType> methods)
    {
        Methods = methods;
    }
}

public class Program
{
    
    static MalType Read() 
    {
        Console.Write("> ");
        var input = Console.ReadLine() ?? "";
        var reader = new Reader();

        var response = reader.ReadString(input);
        
        return response;
    }

    static MalType Evaluate(MalType malType, Environment environment)
    {
        if (malType is not MalListType malListType)
        {
            return EvaluateAst(malType, environment);
        }

        if (malListType.MalTypes.Count == 0) return malListType;

        var result = EvaluateAst(malListType, environment);

        if (result is not MalListType malList)
        {
            throw new Exception("Expected a list");
        }

        result = malList.MalTypes[0];
        
        if (result is not MalFunctionType malFunctionType)
        {
            throw new Exception("Expected a function");
        }

        var invokeResult = malFunctionType.Function.Invoke(malList.Parameters());
        
        return invokeResult;
    }

    static MalType EvaluateAst(MalType ast, Environment environment)
    {
        if (ast is MalListType malListType)
        {
            var list = new List<MalType>();
            
            foreach (var malType in malListType.MalTypes)
            {
                var result = Evaluate(malType, environment);
                list.Add(result);
            }

            var malList = new MalListType(list);

            return malList;
        }

        if (ast is MalSymbolType malSymbolType)
        {
            var lookupResult = environment.Methods[malSymbolType.Symbol];
           
            if (lookupResult is not { } malFunctionType)
            {
                throw new Exception("Expected a function");
            }
            
            return malFunctionType;
        }
        
        return ast;
    }

    static void Print(MalType malType)
    {
        var printer = new Printer();
        Console.WriteLine(printer.PrintString(malType));
    }

    static void ReadEvaluatePrint(Environment environment)
    {
        Print(Evaluate(Read(), environment));
    }
    
    public static void Main(string[] args)
    {
        Environment Standard = new Environment(new Dictionary<Symbol, MalFunctionType>()
        {
            { Symbol.PLUS_SYMBOL, new MalFunctionType(list => (MalNumberType)list[0] + (MalNumberType)list[1])},
            { Symbol.STAR_SYMBOL, new MalFunctionType(list => (MalNumberType)list[0] * (MalNumberType)list[1])},
            { Symbol.MINUS_SYMBOL, new MalFunctionType(list => (MalNumberType)list[0] - (MalNumberType)list[1])},
            { Symbol.DIVIDE_SYMBOL, new MalFunctionType(list => (MalNumberType)list[0] / (MalNumberType)list[1])},
        });
        
        while (true)
        {
            ReadEvaluatePrint(Standard);
        }
    }
}