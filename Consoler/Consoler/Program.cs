using CliWrap;
using CliWrap.Buffered;
using Consoler;
using System.Globalization;
using System.Text;

var arguments = new[] { "update" };

var strBuilder = new StringBuilder();
Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-BR");
Console.WriteLine($"\t\tWINGET");
try
{
    var ctsDots = new CancellationTokenSource();
    _ = Functions.WriteLoader(ctsDots!.Token);
    await Cli.Wrap("winget")
        .WithArguments(arguments)
        .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(Functions.GetUTF8(str))))
        //.WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
        .ExecuteAsync();
        //.ExecuteBufferedAsync(Encoding.Default, Encoding.Latin1);
    ctsDots.Cancel();
    Console.WriteLine();
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
    List<App> appsFoundToUpdate = [];
    for (int i = 2; i < lines.Length && lines[i]?.Length >= indexID + IDSize && lines[i].Contains("winget"); i++)
    {
        var appName = lines[i][..(indexID - 1)].TrimEnd();
        while (appName.Length > 0 && (!char.IsAsciiLetter(appName[^1]) || char.IsWhiteSpace(appName[^1])))
        {
            appName = appName[..^1];
        }
        if (appName.Length == 0)
        {
            Console.WriteLine($"Erro ao ler ({lines[i]})");
            continue;
        }
        int adjustedIndexID = indexID;
        while (adjustedIndexID < lines[i].Length-1 && (!char.IsAsciiLetter(lines[i][adjustedIndexID]) || char.IsWhiteSpace(lines[i][adjustedIndexID])))
        {
            adjustedIndexID++;
        }
        if (adjustedIndexID > indexID + 4)
        {
            Console.WriteLine($"Erro ao ler {appName} ({lines[i]})");
            continue;
        }

        var lineTrailSplit = lines[i][adjustedIndexID..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lineTrailSplit.Length < 3)
        {
            Console.WriteLine($"Erro ao ler {appName} ({lines[i]})");
            continue;
        }

        //else if (!char.IsDigit(lineTrailSplit[1][0]) && char.IsDigit(lineTrailSplit[2][0]) && char.IsDigit(lineTrailSplit[3][0]))
        //{
        //    lineTrailSplit = new[] { $"{lineTrailSplit[0]} {lineTrailSplit[1]}", lineTrailSplit[2], lineTrailSplit[3] };
        //}
        var app = new App
        {
            Nome = appName,
            ID = lineTrailSplit[0],
            Versao = lineTrailSplit[1],
            Disponivel = lineTrailSplit[2],
        };
        
        var (column, line) = Console.GetCursorPosition();
        int compare = Functions.VersionComparer(app.Versao, app.Disponivel);
        if (compare > 1)
        {
            Functions.RevertLastWriteEx(column, line);
            continue;
        }
        else if (compare > 0)
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
        Console.Write($"{app.ID,-18} - {app.Nome,-18} {app.Versao} --> {app.Disponivel}");
        Console.ResetColor();
        if(compare < 0)
        {
            Console.WriteLine($" - disponível");
            appsFoundToUpdate.Add(app);
        }
        else
        {
            Console.WriteLine();
        }
    }
    Console.WriteLine();
    Console.WriteLine();
    List<string> appsToUpdate = [];
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
