using Microsoft.Extensions.Configuration;

var switchMappings = new Dictionary<string, string>()
           {
               { "--waitMs", "WaitMs" },
               { "--error", "Error" },
           };

string[] extraArgs = [.. args.Skip(3)];

IConfiguration configuration = new ConfigurationBuilder()
    .AddCommandLine(extraArgs, switchMappings)
    .Build();

int waitMs = int.Parse(configuration["WaitMs"] ?? "0");
bool showError = bool.Parse(configuration["Error"] ?? "false");

Console.WriteLine($"Fake-Winget {string.Join(" ", args)}");

//Functions.ClearCurrentConsoleLine(1);

if (waitMs <= 0)
{
    waitMs = Random.Shared.Next(3000, 6000);
}
Console.WriteLine($"Waiting for {waitMs} milliseconds...");

Thread.Sleep(waitMs);

if (showError)
{
    Console.WriteLine("Error occurred!");
    return 1;
}

return 0;

public class Functions
{
    private static readonly string Spaces = "                                                                                                                                                                  ";

    public static void ClearCurrentConsoleLine(uint extraLinesUp = 0)
    {
        if (Console.CursorTop - extraLinesUp <= 0)
        {
            Console.Clear();
            return;
        }
        var clearLineSpan = Spaces[^Console.WindowWidth..].AsSpan();
        do
        {
            Console.CursorLeft = 0;
            Console.Write(clearLineSpan);
            Console.CursorLeft = 0;
            if (extraLinesUp > 0 && Console.CursorTop > 0)
            {
                Console.CursorTop--;
            }
            //Console.SetCursorPosition(0, Console.CursorTop);
            //Console.Write(clearLineSpan);
            //Console.SetCursorPosition(0, --currentLine);
        } while (extraLinesUp-- > 0);
    }
}
