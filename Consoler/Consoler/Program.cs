using CliWrap;
using CliWrap.Buffered;
using Consoler;
using System.ComponentModel;
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

    //private const int _DAYSTOKEEPLOGS = 14;
    private const int _QTY_LOGS = 10;

    private const string _ignoredAppsFilename = "ignored.dat";

    private static string? _strPath;

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        _strPath = $"{Path.GetDirectoryName(Environment.ProcessPath)}{Path.DirectorySeparatorChar}";
        var strBuilder = new StringBuilder();
        Console.WriteLine($"\t\tWINGET");
        var ctsDots = new CancellationTokenSource();
        var ctsMain = new CancellationTokenSource();
        try
        {
            _ = Functions.WriteLoader(ctsDots!.Token);
            await Cli.Wrap("winget")
                .WithArguments("upgrade")
                .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
            ctsDots.Cancel();
            Console.Write("\b");
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
                await LogsMaintenanceAsync(ctsMain!.Token).ConfigureAwait(false);
                await File.AppendAllTextAsync($"{_strPath}wingetOutput_{DateTime.Now.MyFileTimestamp()}.txt", fullOutput, ctsMain!.Token)
                    .ConfigureAwait(false);
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

            await ProcessListAsync(fullOutput, ctsMain!.Token).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            ctsDots.Cancel();
            ctsMain.Cancel();
            Console.Write("\b");
            Console.WriteLine("\bErro ao atualizar...");
            Console.WriteLine(ex.ToString());
            try
            {
                File.AppendAllText($"{_strPath}exception_main_{DateTime.Now.MyFileTimestamp()}.txt", ex.ToString());
            }
            catch
            {
            }
            Console.ReadLine();
            Environment.FailFast(ex.ToString());
        }
    }

    private static async Task ProcessListAsync(string fullOutput, CancellationToken token)
    {
        var content = fullOutput[fullOutput.IndexOf("Nome")..];
        var lines = content.Split(Environment.NewLine);
        int indexID = lines[0].IndexOf("ID");
        int IDSize = lines[0].IndexOf("Vers") - indexID;
        int indexVersao = lines[0].IndexOf("Vers");
        //int VersaoSize = lines[0].IndexOf("Dispon") - indexVersao;

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
        int namePaddingLength = 18;
        int idPaddingLength = 10;
        for (int i = 2; i < lines.Length && lines[i]?.Length >= indexID + IDSize && lines[i].Contains("winget"); i++)
        {
            var appName = lines[i][..(indexID - 1)].TrimEnd();
            while (appName.Length > 0 && !char.IsBetween(appName[^1], '!', '~'))
            {
                appName = appName[..^1];
            }
            if (appName.Length == 0)
            {
                Console.WriteLine($"Erro ao ler ({lines[i]})");
                continue;
            }

            int adjustedIndexID = indexID;
            while (adjustedIndexID < lines[i].Length - 1 && !char.IsBetween(appName[^1], '!', '~'))
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
            if (lineTrailSplit.Length >= 4 && lineTrailSplit[1] == "<")
            {
                appsFoundToUpdate.Add(new App
                {
                    Nome = appName,
                    ID = lineTrailSplit[0],
                    Versao = $"{lineTrailSplit[1]} {lineTrailSplit[2]}",
                    Disponivel = lineTrailSplit[3],
                });
            }
            else
            {
                appsFoundToUpdate.Add(new App
                {
                    Nome = appName,
                    ID = lineTrailSplit[0],
                    Versao = lineTrailSplit[1],
                    Disponivel = lineTrailSplit[2],
                });
            }

            //Padding
            if (appName.Length > namePaddingLength)
            {
                namePaddingLength = appName.Length;
            }
            if (lineTrailSplit[0].Length > idPaddingLength)
            {
                idPaddingLength = lineTrailSplit[0].Length;
            }
        }
        lines = null;

        if (appsFoundToUpdate.Count == 0)
        {
            Console.WriteLine("Nenhum app encontrado para atualizar");
            return;
        }
        List<App> appsAbleToUpdate = [];
        string nameFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Nome.Length)}}}";
        string idFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.ID.Length)}}}";
        string curVerFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Versao.Length)}}}";
        string avlbVerFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Disponivel.Length)}}}";
        foreach (var app in appsFoundToUpdate)
        {
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
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    appsAbleToUpdate.Add(app);
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                appsAbleToUpdate.Add(app);
            }

            Console.Write(idFmt, app.ID);
            Console.Write(" ");
            Console.Write(curVerFmt, app.Versao);
            Console.Write(" --> ");
            Console.Write(avlbVerFmt, app.Disponivel);
            Console.ResetColor();
            if (compare <= 0)
            {
                Console.Write($" - disponível");
                if (isIgnored)
                {
                    Console.Write($" PORÉM ignorado...");
                }
                else if (compare == 0)
                {
                    Console.Write($" - Por sua conta e Risco...");
                }
            }
            Console.WriteLine();
        }
        appsFoundToUpdate.Shrink();
        appsToIgnore.Shrink();

        Console.WriteLine();
        Console.WriteLine();

        List<string> appsToUpdate = [];
        foreach (var app in appsAbleToUpdate)
        {
            var (column, line) = Console.GetCursorPosition();
            Console.Write($"Atualizar {app.Nome}? [Y]es/[S]im - [I]gnore/I[g]norar  No/Não[Any Other]: ");

            var key = Console.ReadKey(true);
            //Console.Write("\b");
            Console.WriteLine();
            Functions.RevertLastWriteEx(column, line);
            if (key.Key is ConsoleKey.Y or ConsoleKey.S)
            {
                Console.WriteLine($"{app.Nome} adicionado");
                appsToUpdate.Add(app.ID);
            }
            else if (key.Key is ConsoleKey.I or ConsoleKey.G)
            {
                try
                {
                    await File.AppendAllTextAsync(_ignoredAppsFilename, $"{app.ID} {app.Versao} {app.Disponivel}{Environment.NewLine}", token).ConfigureAwait(false);
                    Console.WriteLine($"{app.Nome} ignorado");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Add App {app.ID} {app.Versao} {app.Disponivel} EXCEPTION: {ex.Message}");
                }
            }
        }

        int iUpdates = await ProcessUpdatesAsync(appsToUpdate, token).ConfigureAwait(false);
        if (iUpdates == 0)
        {
            Console.WriteLine("Nenhum update disponível");
        }
        else
        {
            Console.WriteLine($"{iUpdates} atualizaç{(iUpdates == 1 ? "ão" : "ões")} realizada{(iUpdates == 1 ? "" : "s")}");
        }


        await Task.WhenAny([Task.Delay(iUpdates == 0 ? 2000 : 5000), Task.Run(Console.ReadKey)])
            .ConfigureAwait(false);
    }

    private static async Task<int> ProcessUpdatesAsync(List<string> appsToUpdate, CancellationToken token)
    {
        int iUpdates = 0;
        var strBuilder = new StringBuilder();
        foreach (string appID in appsToUpdate)
        {
            var ctsDotsUpdate = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                Console.Write($"Atualizando {appID} ");
                _ = Functions.WriteLoader(ctsDotsUpdate!.Token);
                await Cli.Wrap("winget")
                    .WithArguments(["update", appID, "--silent"])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                    .ExecuteAsync(token)
                    .ConfigureAwait(false);
                ctsDotsUpdate.Cancel();
                Console.Write("\b");
                Console.WriteLine();
                Console.WriteLine($"{appID} atualizado");
                iUpdates++;
                strBuilder.Clear();
            }
            catch (Exception ex)
            {
                ctsDotsUpdate.Cancel();
                Console.Write("\b");
                Console.WriteLine();
                Console.WriteLine($"{appID} EXCEPTION");
                Console.WriteLine(strBuilder.ToString());
                try
                {
                    await File.AppendAllTextAsync($"{_strPath}exception_update_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now.Ticks:X16}.txt", ex.ToString(), token).ConfigureAwait(false);
                }
                catch
                {
                }
                Console.ReadKey(true);
            }

            Console.WriteLine();
            Console.WriteLine();
        }

        return iUpdates;
    }
    private static async Task LogsMaintenanceAsync(CancellationToken token)
    {
        //scan for old logs
        var files = Directory.GetFiles(_strPath!, "wingetOutput_*.txt");
        if (files.Length > _QTY_LOGS)
        {
            Array.Sort(files);

            await Task.WhenAll(files[..^_QTY_LOGS].Select(async file => await Task.Run(() =>
            {
                try
                {
                    //Console.WriteLine($"Deletando {file}..");
                    //Task.Delay(5000).Wait();
                    File.Delete(file);
                    //Console.WriteLine($"Deletou {file}..");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\bErro ao deletar {file}: {ex}");
                }
            }).ConfigureAwait(false))).ConfigureAwait(false);

        }
    }
}
