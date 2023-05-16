namespace IIIFServerSmokeTest;

public class Result
{
    private List<string> messages = new List<string>();

    public bool Success { get; private set; } = true;
    
    public string[] Messages => messages.ToArray();

    public void Add(string message)
    {
        messages.Add(message);
        Console.WriteLine(message);
    }

    public void AddFailure(string message)
    {
        Success = false;
        messages.Add(message);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}