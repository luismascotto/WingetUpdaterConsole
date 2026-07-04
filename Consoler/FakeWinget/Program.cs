
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