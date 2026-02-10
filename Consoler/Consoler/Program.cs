using CliWrap;
using Microsoft.Extensions.Configuration;
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

    private static bool _DebugLoader;
    private static int _LoaderType;
    private static string? _LoaderSymbols;

    private static async Task Main(string[] args)
    {
        var switchMappings = new Dictionary<string, string>()
           {
               { "--debug", "DebugLoader" },
               { "--loader", "LoaderType" },
               { "--symbols", "LoaderSymbols" },
           };

        _strPath = $"{Path.GetDirectoryName(Environment.ProcessPath)}{Path.DirectorySeparatorChar}";

        IConfiguration config = new ConfigurationBuilder()
            .AddCommandLine(args, switchMappings)
            .Build();

        if (!bool.TryParse(config["DebugLoader"] ?? "false", out _DebugLoader))
        {
            throw new ArgumentException("Invalid DebugLoader value");
        }
        if (!int.TryParse(config["LoaderType"] ?? "1", out _LoaderType))
        {
            throw new ArgumentException("Invalid LoaderType value");
        }

        _LoaderSymbols = config["LoaderSymbols"] ?? "";

        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        if (_DebugLoader)
        {
            Console.Title = "winget-upgrade-debug";
            Console.WriteLine($"\t\tWINGET (Debug Loader)");

            ProcessDebugLoader(_LoaderType, _LoaderSymbols);
            return;
        }
        Console.Title = "winget-upgrade";
        Console.WriteLine($"\t\tWINGET");
        var ctsLoader = new CancellationTokenSource();
        var ctsMain = new CancellationTokenSource();
        try
        {
            switch (_LoaderType)
            {
                case 1:
                    _ = Loader.Wait(ctsLoader!.Token);
                    break;
                case 2:
                    _ = Loader.Wait2(_LoaderSymbols, ctsLoader!.Token);
                    break;
            }
            //_ = Loader.Wait2(ctsLoader!.Token);
            if (_DebugLoader)
            {
                await Functions.WaitEnterKeyUpTo(600000);
                Loader.Stop(ctsLoader);
                return;
            }
            var strBuilder = new StringBuilder();
            _ = await Cli.Wrap("winget")
                .WithArguments("upgrade")
                .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
            Loader.Stop(ctsLoader);
            //Functions.ClearCurrentConsoleLine();
            if (strBuilder.Length == 0)
            {
                //Console.WriteLine("EMPTY");
                return;
            }

            Console.WriteLine();
            Console.WriteLine();
            string fullOutput = strBuilder.ToString();
            await LogsMaintenanceAsync().ConfigureAwait(false);
            await File.AppendAllTextAsync($"{_strPath}wingetOutput_{DateTime.Now.MyFileTimestamp()}.txt", fullOutput, CancellationToken.None)
                .ConfigureAwait(false);

            await ProcessListAsync(fullOutput, CancellationToken.None).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            Loader.Stop(ctsLoader);
            try
            {
                File.AppendAllText($"{_strPath}exception_main_{DateTime.Now.MyFileTimestamp()}.txt", ex.ToString());
            }
            catch
            {
            }
            ex.PrintAndWait("Erro ao atualizar...");

            //Environment.FailFast(ex.ToString());
        }
    }

    private static async Task ProcessListAsync(string fullOutput, CancellationToken token)
    {
        string content = "";
        if (fullOutput.Contains("Nome"))
        {
            content = fullOutput[fullOutput.IndexOf("Nome")..];
        }

        var lines = content.Split(Environment.NewLine);
        int indexID = lines[0].IndexOf("ID");
        int IDSize = lines[0].IndexOf("Vers") - indexID;
        //int indexVersao = lines[0].IndexOf("Vers");
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
            var appToAdd = new App
            {
                Nome = appName,
                ID = lineTrailSplit[0],
            };
            if (lineTrailSplit.Length >= 4 && lineTrailSplit[1] == "<")
            {
                appToAdd.Versao = $"{lineTrailSplit[1]} {lineTrailSplit[2]}";
                appToAdd.Disponivel = lineTrailSplit[3];
            }
            else
            {
                appToAdd.Versao = lineTrailSplit[1];
                appToAdd.Disponivel = lineTrailSplit[2];
            }
            appsFoundToUpdate.Add(appToAdd);

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
            await Functions.WaitEnterKeyUpTo(2000);
            return;
        }
        List<App> appsUpdateable = [];
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
                    appsUpdateable.Add(app);
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                appsUpdateable.Add(app);
            }

            Console.Write(idFmt, app.ID);
            Console.Write(" ");
            Console.Write(curVerFmt, app.Versao);
            Console.Write(" --> ");
            int rightPadding = appsFoundToUpdate.Max(found => found.Disponivel.Length) - app.Disponivel.Length;
            PrintNewVersion(app, rightPadding);
            //Console.Write(avlbVerFmt, app.Disponivel);
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
                else if (compare < -1)
                {
                    Console.Write($" - Atenção*");
                }
            }
            Console.WriteLine();
        }
        appsFoundToUpdate.Shrink();
        appsToIgnore.Shrink();

        Console.WriteLine();
        Console.WriteLine();

        List<string> appsToUpdate = [];
        foreach (var app in appsUpdateable)
        {
            var (column, line) = Console.GetCursorPosition();
            Console.Write($"Atualizar {app.Nome}? [Y]es/[S]im - [I]gnore/I[g]norar  No/Não[Any Other]: ");

            var key = Console.ReadKey(true);
            //Console.Write("\b");
            Console.WriteLine();
            Functions.RevertLastWrite(column, line);
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

        if (appsToUpdate.Count == 0)
        {
            Console.WriteLine("Nenhum update escolhido...");
            await Functions.WaitEnterKeyUpTo(1000);
            return;
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

        await Functions.WaitEnterKeyUpTo(iUpdates == 0 ? 2000 : 4000);
    }

    private static void PrintNewVersion(App app, int disponivelPad)
    {
        int iCompVersion = 0;
        var currSemVer = app.Versao.Split('.');
        var nextSemVer = app.Disponivel.Split('.');
        //if (currSemVer.Length != nextSemVer.Length)
        //{
        //    //different length, print all in yellow
        //    Console.ForegroundColor = ConsoleColor.Yellow;
        //    Console.Write(app.Disponivel);
        //    if (disponivelPad > 0)
        //    {
        //        Console.Write(new string(' ', disponivelPad));
        //    }
        //    return;
        //}
        while (iCompVersion < nextSemVer.Length)
        {
            if (iCompVersion >= currSemVer.Length)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
            }
            else if (currSemVer[iCompVersion] != nextSemVer[iCompVersion])
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            Console.Write(nextSemVer[iCompVersion]);
            iCompVersion++;
            if (iCompVersion < nextSemVer.Length)
            {
                Console.Write(".");
            }
        }

        if (disponivelPad > 0)
        {
            Console.Write(new string(' ', disponivelPad));
        }
    }

    private static async Task<int> ProcessUpdatesAsync(List<string> appsToUpdate, CancellationToken token)
    {
        if (!appsToUpdate.AnySafe())
        {
            return 0;
        }
        int iUpdates = 0;
        var strBuilder = new StringBuilder();
        foreach (string appID in appsToUpdate)
        {
            var ctsLoader = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                Console.Write($"Atualizando {appID} ");
                switch (_LoaderType)
                {
                    case 1:
                        _ = Loader.Wait(ctsLoader!.Token);
                        break;
                    case 2:
                        _ = Loader.Wait2(_LoaderSymbols!, ctsLoader!.Token);
                        break;
                }
                //_ = Loader.Wait(ctsLoader!.Token);
                _ = await Cli.Wrap("winget")
                    .WithArguments(["update", appID, "--silent"])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                    .ExecuteAsync(token)
                    .ConfigureAwait(false);
                Loader.Stop(ctsLoader);
                //ctsLoader.Cancel();
                //await Task.Delay(50, CancellationToken.None);
                //Console.Write("\b  ");

                Console.WriteLine();
                Console.WriteLine($"{appID} atualizado");
                iUpdates++;
                _ = strBuilder.Clear();
            }
            catch (Exception ex)
            {
                Loader.Stop(ctsLoader);
                //ctsLoader.Cancel();
                //await Task.Delay(50, CancellationToken.None);
                //Console.Write("\b  ");

                Console.WriteLine();
                Console.WriteLine($"{appID} EXCEPTION");
                Console.WriteLine(strBuilder.ToString());
                try
                {
                    await File.AppendAllTextAsync($"{_strPath}exception_update_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now.Ticks:X16}.txt",
                        ex.ToString(),
                        CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                }
                await Functions.WaitEnterKeyUpTo(30000);
            }
            //Console.WriteLine();
        }

        return iUpdates;
    }

    private static async Task LogsMaintenanceAsync()
    {
        //scan for old logs
        var files = Directory.GetFiles(_strPath!, "wingetOutput_*.txt");
        if (files.Length > _QTY_LOGS)
        {
            //Sort files by Creation Date
            Array.Sort(files, (f1, f2) => File.GetCreationTime(f1).CompareTo(File.GetCreationTime(f2)));

            //Keep only last _QTY_LOGS files (and delete the rest)
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
                    Console.WriteLine($"Erro ao deletar {file}: {ex}");
                }
            }).ConfigureAwait(false))).ConfigureAwait(false);
        }
    }

    private static void ProcessDebugLoader(int loaderType, string loaderSymbols)
    {
        int iTry = 5;
        while (iTry-- > 0)
        {
            Console.Write($"{Environment.NewLine}ENTER to RELOAD ({iTry + 1}x exit) ");

            var ctsLoader = new CancellationTokenSource();
            try
            {
                switch (loaderType)
                {
                    case 1:
                        _ = Loader.Wait(ctsLoader!.Token);
                        break;
                    case 2:
                        _ = Loader.Wait2(loaderSymbols, ctsLoader!.Token);
                        break;
                }
                Functions.WaitEnterKeyUpTo(600000).Wait();
                Loader.Stop(ctsLoader);

            }
            catch
            {
                Loader.Stop(ctsLoader);
                break;
            }
        }
    }
}
