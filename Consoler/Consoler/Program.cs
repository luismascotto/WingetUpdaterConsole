using CliWrap;
using Consoler;
using System.Globalization;
using System.Text;

var strBuilder = new StringBuilder();
Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
Console.WriteLine($"\t\tWINGET");
try
{
    var ctsDots = new CancellationTokenSource();
    _ = Functions.WriteLoader(ctsDots!.Token);
    await Cli.Wrap("winget")
        .WithArguments(new[] { "update" })
        .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(Functions.GetWindows1252fromUtf8(str))))
        .ExecuteAsync();
    ctsDots.Cancel();
    Console.WriteLine();

    //Console.WriteLine(strBuilder.ToString());

    var fullOutput = strBuilder.ToString();

    if (string.IsNullOrWhiteSpace(fullOutput))
    {
        Console.WriteLine("EMPTY");
        return;
    }
    if (!fullOutput.Contains("Nome") || !fullOutput.Contains("ID"))
    {
        Console.WriteLine("Dados nao conferem");
        return;
    }
    var content = fullOutput[fullOutput.IndexOf("Nome")..];
    var lines = content.Split(Environment.NewLine);
    int indexID = lines[0].IndexOf("ID");
    int IDSize = lines[0].IndexOf("Vers") - indexID;
    int indexVersao = lines[0].IndexOf("Vers");
    int VersaoSize = lines[0].IndexOf("Dispon") - indexVersao;

    int iUpdates = 0;
    List<App> appsFoundToUpdate = new();
    for (int i = 2; i < lines.Length && lines[i]?.Length >= indexID + IDSize && lines[i].Contains("winget"); i++)
    {
        var appName = lines[i][..(indexID - 1)].TrimEnd();
        var lineTrailSplit = lines[i][indexID..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lineTrailSplit.Length < 3)
        {
            Console.WriteLine($"Erro ao ler {appName} ({lines[i]})");
            continue;
        }
        var app = new App
        {
            Nome = appName,
            ID = lineTrailSplit[0],
            Versao = lineTrailSplit[1],
            Disponivel = lineTrailSplit[2],
        };

        Console.Write($"{app.ID,-18} - {app.Nome,-18} ");

        int minSize = Math.Min(app.Versao.Length, app.Disponivel.Length);
        int compare = string.Compare(app.Versao[..minSize], app.Disponivel[..minSize], StringComparison.InvariantCultureIgnoreCase);
        if (compare > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        else if (compare < 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        Console.Write($"{app.Versao} --> {app.Disponivel}");
        Console.ResetColor();
        Console.WriteLine($" - disponível");

        appsFoundToUpdate.Add(app);
    }
    Console.WriteLine();
    Console.WriteLine();
    List<string> appsToUpdate = new();
    foreach (var app in appsFoundToUpdate)
    {
        Console.Write($"Atualizar {app.Nome}? Y/N: ");
        var key = Console.ReadKey(true);
        Console.Write("\b");
        Console.WriteLine();
        if (key.Key == ConsoleKey.Y)
        {
            Console.WriteLine($"{app.Nome} adicionado");
            appsToUpdate.Add(app.ID);
        }
    }

    foreach (string appID in appsToUpdate)
    {
        var ctsDotsUpdate = new CancellationTokenSource();
        try
        {
            Console.WriteLine();
            Console.Write($"Atualizando {appID} ");
            _ = Functions.WriteLoader(ctsDotsUpdate!.Token);
            await Cli.Wrap("winget").WithArguments(new[] { "update", appID }).ExecuteAsync();
            Console.WriteLine();
            Console.WriteLine($"{appID} atualizado");
            iUpdates++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{appID} EXCEPTION");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            ctsDotsUpdate.Cancel();
        }

        Console.WriteLine();
        Console.WriteLine();

    }
    if (iUpdates == 0)
    {
        Console.WriteLine("Nenhum update disponivel");
    }
    else
    {
        Console.WriteLine($"{iUpdates} atualizac{(iUpdates == 1 ? "ao" : "oes")} realizada{(iUpdates == 1 ? "" : "s")}");
    }


    Task.WaitAny(new[] { Task.Delay(5000), Task.Run(() => Console.ReadKey()) });

}
catch (Exception ex)
{
    Console.WriteLine("Erro ao atualizar...");
    Console.WriteLine(ex.Message);
    Environment.FailFast("!");
}

struct App
{
    public string Nome;
    public string ID;
    public string Versao;
    public string Disponivel;
}
