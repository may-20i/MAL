using System.Text.RegularExpressions;

namespace Mal;

#region MalTypes

internal abstract class MalType
{}

internal abstract class MalAtomic : MalType
{}

internal class MalFunctionType : MalType
{
    internal Func<MalListType, MalType> Function;
    internal bool IsMacro = false;

    public MalFunctionType(Func<MalListType, MalType> function)
    {
        Function = function;
    }
}

internal class MalSymbolType : MalAtomic
{
    internal string Symbol;
    
    internal MalSymbolType(string symbol)
    {
        Symbol = symbol;
    }
}

internal class MalStringType : MalAtomic
{
    internal string Text;
    
    public MalStringType(string text)
    {
        Text = text;
    }
}

internal class MalBooleanType : MalAtomic
{
    internal bool Boolean;
    
    public MalBooleanType(bool boolean)
    {
        Boolean = boolean;
    }
}

internal class MalNullType : MalAtomic {}

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

    public MalType this[int i]
    {
        get => MalTypes[i];
        set => MalTypes[i] = value;
    }
}

internal class MalAtomType : MalType
{
    internal MalType MalType;

    public MalAtomType(MalType malType)
    {
        MalType = malType;
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
    private int _position;

    private readonly Regex _malRegex =
        new(@"[\s,]*(~@|[\[\]{}()'`~^@]|""(?:\\.|[^\\""])*""?|;.*|[^\s\[\]{}('""`,;)]*)");

    private readonly List<Token> _tokens;

    private Reader(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public Reader()
    {
        _tokens = new List<Token>();
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
        
        var list = new List<MalType>();

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
        MalAtomic malAtomic;

        malAtomic = token.Text[0] switch
        {
            ')' => new MalSymbolType(")"),
            '+' => new MalSymbolType("+"),
            '*' => new MalSymbolType("*"),
            '-' => new MalSymbolType("-"),
            '"' => ReadString(),
            >= '0' and <= '9' => ReadNumber(),
            _ => ReadKeyword()
        };

        return malAtomic;
    }

    private MalStringType ReadString()
    {
        var token = Peek();

        var text = token.Text;

        if (text[^1] != '"')
        {
            throw new Exception($"Expected a string but found {token.Text}");
        }

        return new MalStringType(text[1..^1]);
    }

    private MalAtomic ReadKeyword()
    {
        var token = Peek();

        MalAtomic malAtomic = token.Text switch
        {
            "true" => new MalBooleanType(true),
            "false" => new MalBooleanType(false),
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
            "'" => Quote(),
            _ => ReadAtomic()
        };

        return result;
    }
    
    private MalListType Quote()
    {
        Next();
        var quoted = new MalListType(new List<MalType>());
        quoted.MalTypes.Add(new MalSymbolType("quote"));
        quoted.MalTypes.Add(ReadForm());
        return quoted;
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

            return " ( " + string.Join(" ", results.ToArray()) + " ) ";
        }

        if (malType is MalNumberType malNumberType)
        {
            return malNumberType.Number.ToString();
        }

        if (malType is MalFunctionType malFunctionType)
        {
            return $"<Function> {malFunctionType.Function} </Function>";
        }

        if (malType is MalBooleanType malBooleanType)
        {
            return malBooleanType.Boolean.ToString();
        }

        if (malType is MalNullType)
        {
            return "null";
        }

        if (malType is MalStringType malStringType)
        {
            return malStringType.Text;
        }
        
        if (malType is MalAtomType malAtomType)
        {
            return $"atom => {PrintString(malAtomType.MalType)}";
        }

        throw new NotImplementedException($"String type not implemented for {malType}");
    }
    
    internal MalNullType Print(MalType malType)
    {
        Console.WriteLine(PrintString(malType));
        return new MalNullType();
    }
}

#endregion

internal class Environment
{
    internal Dictionary<string, MalType> Data;
    internal Environment? Outer;

    public Environment(Dictionary<string, MalType> data, Environment? outer = null, MalListType? binds = null, MalListType? expressions = null)
    {
        Data = data;
        Outer = outer;
        
        if ((binds?.MalTypes.Count ?? 0) != (expressions?.MalTypes.Count ?? 0))
        {
            throw new ArgumentException("Binds must match expressions.");
        }

        if (binds is not null && expressions is not null)
        {
            foreach (var malType in binds.MalTypes)
            {
                var symbol = (MalSymbolType)malType;
                var index = binds.MalTypes.IndexOf(malType);
                var expression = expressions[index];
                Set(symbol.Symbol, expression);
            }
        }
        
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

    public MalType Get(string symbol)
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

public abstract class Program
{
    private static MalType Read() 
    {
        Console.Write("> ");
        var input = Console.ReadLine() ?? "";
        var reader = new Reader();
        
        var response = reader.ReadString(input);
        
        
        return response;
    }

    private static bool IsMacroCall(MalType ast, Environment environment)
    {
        if (ast is not MalListType malListType) return false;
        if (malListType.MalTypes.Count == 0) return false;

        var first = malListType.MalTypes[0];

        if (first is not MalSymbolType malSymbolType) return false;

        try
        {
            var result = environment.Get(malSymbolType.Symbol);

            if (result is not MalFunctionType malFunctionType) return false;

            return malFunctionType.IsMacro;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static MalType MacroExpand(MalType ast, Environment environment)
    {
        while (IsMacroCall(ast, environment))
        {
            var malListType = (MalListType)ast;
            var first = (MalSymbolType)malListType.MalTypes[0];
            var macro = (MalFunctionType)environment.Get(first.Symbol);
            var expanded = macro.Function.Invoke(malListType.Parameters());
            
            ast = expanded;
        }

        return ast;
    }

    private static MalType Evaluate(MalType malType, Environment environment)
    {
        if (malType is not MalListType ast) return EvaluateAst(malType, environment);
        if (ast.MalTypes.Count == 0) return ast;

        ast = (MalListType)MacroExpand(ast, environment);
        
        if (ast is not MalListType) return EvaluateAst(ast, environment);
        
        switch (ast.MalTypes[0])
        {
            // special forms.
            case MalSymbolType { Symbol: "def!" }:
                environment.Set(((MalSymbolType)ast[1]).Symbol, Evaluate(ast[2], environment));
                return environment.Get(((MalSymbolType)ast[1]).Symbol);
            case MalSymbolType { Symbol: "let*" }:
            {
                var newEnvironment = new Environment(new Dictionary<string, MalType>(), environment);
                var bindings = (MalListType)ast[1];
                for (var i = 0; i < bindings.MalTypes.Count; i += 2)
                {
                    newEnvironment.Set(((MalSymbolType)bindings[i]).Symbol, Evaluate(bindings[i + 1], newEnvironment));
                }
                
                return Evaluate(ast[2], newEnvironment);
            }
            case MalSymbolType { Symbol: "do" }:
            {
                MalType doResult = null!;
                
                foreach (var type in ast.Parameters().MalTypes)
                {
                    doResult = Evaluate(type, environment);
                }

                return doResult;
            }
            case MalSymbolType { Symbol: "if" }:
            {
                var condition = Evaluate(ast[1], environment);

                if (condition is not MalBooleanType malBooleanType)
                    return condition is MalNullType
                        ? Evaluate(ast[3], environment)
                        : Evaluate(ast[2], environment);
                
                return malBooleanType.Boolean ? Evaluate(ast[2], environment) : Evaluate(ast[3], environment);
            }
            case MalSymbolType { Symbol: "fn*" }:
            {
                var newFunction = new MalFunctionType((l) =>
                {
                    var newEnvironment = new Environment(new Dictionary<string, MalType>(), environment, (MalListType)ast[1], l);
                    return Evaluate(ast[2], newEnvironment);
                });

                return newFunction;
            }
            case MalSymbolType { Symbol: "quote" }:
            {
                return ast[1];
            }
            case MalSymbolType { Symbol: "defmacro!" }:
            {
                var malFunction = (MalFunctionType)Evaluate(ast[2], environment);
                malFunction.IsMacro = true;
                
                environment.Set(((MalSymbolType)ast[1]).Symbol, malFunction);
                return environment.Get(((MalSymbolType)ast[1]).Symbol);
            }
            case MalSymbolType { Symbol: "macroexpand" }:
            {
                var first = ast[1];
                return MacroExpand(first, environment);
            }
        }

        var result = EvaluateAst(ast, environment);

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

    private static MalType EvaluateAst(MalType ast, Environment environment)
    {
        if (ast is MalListType malListType)
        {
            var list = malListType.MalTypes.Select(malType => Evaluate(malType, environment)).ToList();

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

    private static void Print(MalType malType)
    {
        var printer = new Printer();
        Console.WriteLine(printer.PrintString(malType));
    }

    private static void ReadEvaluatePrint(Environment environment)
    {
        Print(Evaluate(Read(), environment));
    }
    
    public static void Main(string[] args)
    {
        var standard = new Environment(
            new Dictionary<string, MalType>());
        var reader = new Reader();
        
        standard.Set("+", new MalFunctionType(list => (MalNumberType)list[0] + (MalNumberType)list[1]));
        standard.Set("-", new MalFunctionType(list => (MalNumberType)list[0] - (MalNumberType)list[1]));
        standard.Set("*", new MalFunctionType(list => (MalNumberType)list[0] * (MalNumberType)list[1]));
        standard.Set("/", new MalFunctionType(list => (MalNumberType)list[0] / (MalNumberType)list[1]));
        standard.Set("list", new MalFunctionType(list => new MalListType(list.MalTypes)));
        standard.Set("list?", new MalFunctionType(list => new MalBooleanType(list[0] is MalListType)));
        standard.Set("empty?", new MalFunctionType(list => new MalBooleanType(((MalListType)list[0]).MalTypes.Count == 0)));
        standard.Set("count", new MalFunctionType(list => new MalNumberType(((MalListType)list[0]).MalTypes.Count)));
        standard.Set("=", new MalFunctionType(list => new MalBooleanType(list[0] == list[1])));
        standard.Set("<", new MalFunctionType(list => new MalBooleanType(((MalNumberType)list[0]).Number < ((MalNumberType)list[1]).Number)));
        standard.Set("<=", new MalFunctionType(list => new MalBooleanType(((MalNumberType)list[0]).Number <= ((MalNumberType)list[1]).Number)));
        standard.Set(">", new MalFunctionType(list => new MalBooleanType(((MalNumberType)list[0]).Number > ((MalNumberType)list[1]).Number)));
        standard.Set(">=", new MalFunctionType(list => new MalBooleanType(((MalNumberType)list[0]).Number >= ((MalNumberType)list[1]).Number)));
        standard.Set("str", new MalFunctionType(list => new MalStringType(((MalStringType)list[0]).Text)));
        standard.Set("atom", new MalFunctionType(list => new MalAtomType(list[0])));
        standard.Set("atom?", new MalFunctionType(list => new MalBooleanType(list[0] is MalAtomType)));
        standard.Set("deref", new MalFunctionType(list => ((MalAtomType)list[0]).MalType));
        standard.Set("cons", new MalFunctionType(list => new MalListType(((MalListType)list[0]).MalTypes.Concat(((MalListType)list[1]).MalTypes).ToList())));
        standard.Set("concat", new MalFunctionType(list =>
        {
            var result = new MalListType(new List<MalType>());
            foreach (var type in list.MalTypes.SelectMany(malType => ((MalListType)malType).MalTypes))
            {
                result.MalTypes.Add(type);
            }

            return result;
        }));
        standard.Set("reset!", new MalFunctionType(list =>
        {
            ((MalAtomType)list[0]).MalType = list[1];
            return ((MalAtomType)list[0]).MalType;
        }));
        standard.Set("print", new MalFunctionType(list => new Printer().Print(list[0])));
        standard.Set("swap!", new MalFunctionType(list =>
        {
            var atom = (MalAtomType)list[0];
            var function = (MalFunctionType)list[1];
            var arguments = new MalListType(new List<MalType>(){atom.MalType});
            
            if (list.MalTypes.Count > 2)
            {
                arguments.MalTypes.AddRange(list.MalTypes.Skip(2));
            }
            
            var result = function.Function.Invoke(arguments);
            atom.MalType = result;
            return atom.MalType;
        }));
        standard.Set("combine-strings", new MalFunctionType(list => new MalStringType(string.Join("", list.MalTypes.Select(m => ((MalStringType)m).Text)))));
        standard.Set("read-string", new MalFunctionType(l => reader.ReadString(((MalStringType)l[0]).Text)));
        standard.Set("slurp", new MalFunctionType(l => new MalStringType(File.ReadAllText(((MalStringType)l[0]).Text))));
        standard.Set("line-slurp", new MalFunctionType(l =>
        {
            var result = new MalListType(new List<MalType>());
            foreach (var line in File.ReadAllLines(((MalStringType)l[0]).Text))
            {
                result.MalTypes.Add(new MalStringType(line));
            }
            
            return result;
        }));
        
        standard.Set("eval", new MalFunctionType(list => Evaluate(list[0], standard)));
        standard.Set("typeof", new MalFunctionType(list => new MalStringType(list[0].GetType().Name)));

        while (true)
        {
            try
            {
                ReadEvaluatePrint(standard);
            } catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}