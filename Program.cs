string Read()
{
    Console.Write("> ");
    var response = Console.ReadLine() ?? "";
    return response;
}

string Evaluate(string s)
{
    return s;
}

void Print(string s)
{
    Console.WriteLine(s);
}

void ReadEvaluatePrint()
{
    Print(Evaluate(Read()));

}

while (true)
{
    ReadEvaluatePrint();
}