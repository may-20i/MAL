﻿using System.Reflection;
using System.Text.RegularExpressions;

namespace Mal;

#region MalTypes

internal abstract class MalType
{}

internal abstract class MalAtomic : MalType
{}

internal enum Symbol
{
    RightParenthesis,
    PlusSymbol,
    StarSymbol,
    LeftParenthesis,
    MinusSymbol,
    DivideSymbol
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
    internal string Symbol;

    internal MalSymbolType(Symbol symbol)
    {
        Symbol = symbol.ToString();
    }
    
    internal MalSymbolType(string symbol)
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
                   case ")":
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
        
        MalAtomic malAtomic = token.Text switch
        {
            ")" => new MalSymbolType(")"),
            "+" => new MalSymbolType("+"),
            "*" => new MalSymbolType("*"),
            "-" => new MalSymbolType("-"),
            
            "0" => ReadNumber(),
            "1" => ReadNumber(),
            "2" => ReadNumber(),
            "3" => ReadNumber(),
            "4" => ReadNumber(),
            "5" => ReadAtomic(),
            "6" => ReadNumber(),
            "7" => ReadNumber(),
            "8" => ReadNumber(),
            "9" => ReadNumber(),
            
            _ => new MalSymbolType(token.Text)
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
            return symbol.Symbol;
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

        if (malType is MalFunctionType malFunctionType)
        {
            return $"<Function> {malFunctionType.Function} </Function>";
        }

        throw new NotImplementedException($"String type not implemented for {malType.ToString()}");
    }
}

#endregion

internal class Environment
{
    internal Dictionary<string, MalType> Data;
    internal Environment? Outer;

    public Environment(Dictionary<string, MalType> data, Environment? outer = null)
    {
        Data = data;
        Outer = outer;
    }

    public void Set(string symbol, MalType malType)
    {
        Data[symbol] = malType;
    }

    public Environment? Find(string symbol)
    {
        if (Data.ContainsKey(symbol))
        {
            return this;
        }

        if (Outer is not null)
        {
            return Outer.Find(symbol);
        }

        return null;
    }

    public MalType? Get(string symbol)
    {
        var environment = Find(symbol);
        
        if (environment is null)
        {
            throw new Exception($"Symbol {symbol} not found");
        }

        var result = environment.Data[symbol];

        return result;
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
        if (malType is MalListType malListType)
        {
            if (malListType.MalTypes.Count == 0) return malListType;

            var first = malListType.MalTypes[0];

            if (first is MalSymbolType { Symbol: "def!" })
            {
                environment.Set(((MalSymbolType)malListType[1]).Symbol, Evaluate((MalType)malListType[2], environment));
                return environment.Get(((MalSymbolType)malListType[1]).Symbol)!;
            }

            if (first is MalSymbolType { Symbol: "let*" })
            {
                var newEnvironment = new Environment(new Dictionary<string, MalType>(), environment);
                var bindings = (MalListType)malListType[1];
                for (var i = 0; i < bindings.MalTypes.Count; i += 2)
                {
                    newEnvironment.Set(((MalSymbolType)bindings[i]).Symbol, Evaluate((MalType)bindings[i + 1], newEnvironment));
                }
                
                return Evaluate((MalType)malListType[2], newEnvironment);
            }

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

        return EvaluateAst(malType, environment);
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
            var lookupResult = environment.Get(malSymbolType.Symbol);
           
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
        Environment Standard = new Environment(
            new Dictionary<string, MalType>());
        
        Standard.Set("+", new MalFunctionType(list => (MalNumberType)list[0] + (MalNumberType)list[1]));
        Standard.Set("-", new MalFunctionType(list => (MalNumberType)list[0] - (MalNumberType)list[1]));
        Standard.Set("*", new MalFunctionType(list => (MalNumberType)list[0] * (MalNumberType)list[1]));
        Standard.Set("/", new MalFunctionType(list => (MalNumberType)list[0] / (MalNumberType)list[1]));
        
        while (true)
        {
            ReadEvaluatePrint(Standard);
        }
    }
}