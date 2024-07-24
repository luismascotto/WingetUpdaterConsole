using CliWrap;
using CliWrap.Buffered;
using Consoler;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Consoler;
public class Program
{
    struct App
    {
        public string Nome;
        public string ID;
        public string Versao;
        public string Disponivel;
    }

    private const string _ignoredAppsFilename = "ignored.dat";
    private static void Main()
    {
        var arguments = new[] { "update" };

        var strBuilder = new StringBuilder();
        Console.WriteLine($"\t\tWINGET");
        try
        {
            var ctsDots = new CancellationTokenSource();
            _ = Functions.WriteLoader(ctsDots!.Token);
            Cli.Wrap("winget")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                .ExecuteAsync().GetAwaiter().GetResult();
            ctsDots.Cancel();

            if (strBuilder.Length == 0)
            {
                Console.WriteLine("EMPTY");
                return;
            }

            Console.WriteLine();
            Console.WriteLine();

            string fullOutput = strBuilder.ToString();
            try
            {
                string strPath = $"{Path.GetDirectoryName(Environment.ProcessPath)}{Path.DirectorySeparatorChar}";
                File.WriteAllLines($"{strPath}wingetOutput_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now.Ticks:X16}.txt", [fullOutput]);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File Write Exception: {ex}");
                Console.ResetColor();
                Console.WriteLine();
            }

            if (!fullOutput.Contains("Nome") || !fullOutput.Contains("ID"))
            {
                Console.WriteLine("Dados não conferem");
                return;
            }

            ProcessList(fullOutput);

        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao atualizar...");
            Console.WriteLine(ex.Message);
            Environment.FailFast(ex.ToString());
        }
    }

    private static void ProcessList(string fullOutput)
    {
        var content = fullOutput[fullOutput.IndexOf("Nome")..];
        var lines = content.Split(Environment.NewLine);
        int indexID = lines[0].IndexOf("ID");
        int IDSize = lines[0].IndexOf("Vers") - indexID;
        int indexVersao = lines[0].IndexOf("Vers");
        int VersaoSize = lines[0].IndexOf("Dispon") - indexVersao;

        List<App> appsToIgnore = [];
        if (File.Exists(_ignoredAppsFilename))
        {
            var ignoredAppsLines = File.ReadAllLines(_ignoredAppsFilename);
            foreach (var ignoreLine in ignoredAppsLines.Where(line => line.Length >= 5))
            {
                var ignoreSplit = ignoreLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ignoreSplit.Length < 3)
                {
                    continue;
                }
                appsToIgnore.Add(new App
                {
                    ID = ignoreSplit[0],
                    Versao = ignoreSplit[1],
                    Disponivel = ignoreSplit[2],
                });
            }
        }

        List<App> appsFoundToUpdate = [];
        for (int i = 2; i < lines.Length && lines[i]?.Length >= indexID + IDSize && lines[i].Contains("winget"); i++)
        {
            var appName = lines[i][..(indexID - 1)].TrimEnd();
            while (appName.Length > 0 && (!char.IsAsciiLetterOrDigit(appName[^1]) || char.IsWhiteSpace(appName[^1])))
            {
                appName = appName[..^1];
            }
            if (appName.Length == 0)
            {
                Console.WriteLine($"Erro ao ler ({lines[i]})");
                continue;
            }

            int adjustedIndexID = indexID;
            while (adjustedIndexID < lines[i].Length - 1 && (!char.IsAsciiLetterOrDigit(lines[i][adjustedIndexID]) || char.IsWhiteSpace(lines[i][adjustedIndexID])))
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

            var app = new App
            {
                Nome = appName,
                ID = lineTrailSplit[0],
                Versao = lineTrailSplit[1],
                Disponivel = lineTrailSplit[2],
            };

            var (column, line) = Console.GetCursorPosition();
            int compare = Functions.VersionComparer(app.Versao, app.Disponivel);
            bool isIgnored = false;
            if (compare > 1)
            {
                Functions.RevertLastWriteEx(column, line);
                continue;
            }

            if (compare > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (compare < 0)
            {
                isIgnored = appsToIgnore.Any(a => a.ID == app.ID && a.Versao == app.Versao && a.Disponivel == app.Disponivel);
                if (isIgnored)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    appsFoundToUpdate.Add(app);
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }


            Console.Write($"{app.ID,-18} - {app.Nome,-18} {app.Versao} --> {app.Disponivel}");
            Console.ResetColor();
            if (compare < 0)
            {
                Console.Write($" - disponível");
                if (isIgnored)
                {
                    Console.Write($" PORÉM ignorado...");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine();
        List<string> appsToUpdate = [];
        foreach (var app in appsFoundToUpdate)
        {
            Console.Write($"Atualizar {app.Nome}? [Y]es/[S]im  - [N]o/N[a]o  - [I]gnore/I[g]norar: ");
            var key = Console.ReadKey(true);
            Console.Write("\b");
            Console.WriteLine();
            if (key.Key is ConsoleKey.Y or ConsoleKey.S)
            {
                Console.WriteLine($"{app.Nome} adicionado");
                appsToUpdate.Add(app.ID);
            }
            else if (key.Key is ConsoleKey.I or ConsoleKey.G)
            {
                try
                {
                    File.AppendAllText(_ignoredAppsFilename, $"{app.ID} {app.Versao} {app.Disponivel}{Environment.NewLine}");
                    Console.WriteLine($"{app.Nome} ignorado");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Add App {app.ID} {app.Versao} {app.Disponivel} EXCEPTION: {ex.Message}");
                }
            }
        }

        int iUpdates = ProcessUpdates(appsToUpdate);
        if (iUpdates == 0)
        {
            Console.WriteLine("Nenhum update disponível");
        }
        else
        {
            Console.WriteLine($"{iUpdates} atualizaç{(iUpdates == 1 ? "ão" : "ões")} realizada{(iUpdates == 1 ? "" : "s")}");
        }


        Task.WaitAny([Task.Delay(iUpdates == 0 ? 3000 : 5000), Task.Run(() => Console.ReadKey())]);
    }

    private static int ProcessUpdates(List<string> appsToUpdate)
    {
        int iUpdates = 0;
        foreach (string appID in appsToUpdate)
        {
            var ctsDotsUpdate = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                Console.Write($"Atualizando {appID} ");
                _ = Functions.WriteLoader(ctsDotsUpdate!.Token);
                Cli.Wrap("winget").WithArguments(new[] { "update", appID }).ExecuteAsync().GetAwaiter().GetResult();
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

        return iUpdates;
    }
}
