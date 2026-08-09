using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using Microsoft.Extensions.Configuration;
using static Consoler.Program;

namespace Consoler;

public class Program
{
    public enum AppListState
    {
        Available,
        Selected,
        Ignored,
        Error
    }

    public struct App
    {
        public string strName;
        public string strID;
        public string strVersion;
        public string strNewVersion;
        public AppListState State;
    }

    private struct Config
    {
        public string WingetCommand;
        public bool DebugLoader;
        public int LoaderType;
        public string LoaderSymbols;
        public string ResponseFile;
        public StringBuilder sbResponse;
    }

    //private const int _DAYSTOKEEPLOGS = 14;
    private const int _QTY_LOGS = 50;

    private const int _QTY_RUNS = 5;
    private const string _EXCEPTION_FILE = "exception";
    private const string _UPD_EXCEPTION_FILE = "update";
    private const string _IGNORE_EXCEPTION_FILE = "ignore";
    private const string _OUTPUT_FILE = "winget_output";

    private const string _STR_Winget = "winget";
    private const string _STR_Update = "upgrade";
    private const string _STR_Ignore = "pin add";
    private const string _STR_FakeWinget = "FakeWinget";

    private static readonly string _strLogPath = Path.Combine($"{Path.GetDirectoryName(Environment.ProcessPath)}", "Logs");

    private static async Task Main(string[] args)
    {
        var switchMappings = new Dictionary<string, string>()
           {
               { "--debug", "DebugLoader" },
               { "--dbg-loader-type", "LoaderType" },
               { "--dbg-symbols", "LoaderSymbols" },
               { "--dbg-response-file", "ResponseFile" },
           };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddCommandLine(args, switchMappings)
            .Build();

        Config config = new()
        {
            sbResponse = new StringBuilder(),
            DebugLoader = bool.Parse(configuration["DebugLoader"] ?? "false"),
            LoaderType = int.Parse(configuration["LoaderType"] ?? "1"),
            LoaderSymbols = configuration["LoaderSymbols"] ?? "",
            ResponseFile = configuration["ResponseFile"] ?? "",
            WingetCommand = _STR_Winget
        };

        Directory.CreateDirectory(_strLogPath);

        if (config.ResponseFile.HasSomething())
        {
            if (!File.Exists(config.ResponseFile))
            {
                throw new FileNotFoundException($"Response file not found: {config.ResponseFile}");
            }
            config.WingetCommand = _STR_FakeWinget;
        }
        //Console.WindowWidth = (int)(Console.LargestWindowWidth * 0.9);
        //Console.WindowHeight = (int)(Console.LargestWindowHeight * 0.9);
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        Console.Title = "winget-upgrade";

        if (config.DebugLoader)
        {
            Console.WriteLine($"\t\tWINGET (Debug Loader)");

            ProcessDebugLoader(config.LoaderType, config.LoaderSymbols);
            return;
        }

        uint runs = 0;
        while (true)
        {
            bool flowControl = await ExecuteUpgradeCheckAsync(config, runs).ConfigureAwait(false);
            if (!flowControl || ++runs > _QTY_RUNS)
            {
                break;
            }
        }
    }

    private static async Task<bool> ExecuteUpgradeCheckAsync(Config config, uint runs)
    {
        Console.Clear();
        Console.WriteLine("\t\tWINGET");
        var ctsLoader = new CancellationTokenSource();
        var ctsMain = new CancellationTokenSource();
        try
        {
            config.sbResponse.Clear();
            try
            {
                _ = Loader.Wait(config.LoaderSymbols, ctsLoader.Token);
                if (config.ResponseFile.HasSomething())
                {
                    config.sbResponse.AppendLine(File.ReadAllText(config.ResponseFile));
                }
                else
                {
                    await Cli.Wrap(config.WingetCommand).WithArguments([_STR_Update], false)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => FilterResponse(ref config.sbResponse, str)))
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                Loader.Stop(ctsLoader);
            }

            if (config.sbResponse.Length == 0)
            {
                if (runs == 0)
                {
                    await Functions.WaitEnterKeyUpTo(1000, "Nenhum update retornado...");
                }
                return false;
            }

            Console.WriteLine();
            Console.WriteLine();
            config.sbResponse.AppendToTimestampFile(_strLogPath, _OUTPUT_FILE);
            await LogsMaintenanceAsync(_OUTPUT_FILE).ConfigureAwait(false);

            var appsToUpdate = ProcessList(config);
            if (appsToUpdate.Count == 0)
            {
                await Functions.WaitEnterKeyUpTo(1000, "Nenhum update encontrado...");
                return false;
            }
            int available = appsToUpdate.Count(app => app.State == AppListState.Available);
            int selected = appsToUpdate.Count(app => app.State == AppListState.Selected);

            if (selected == 0)
            {
                if (available == 0)
                {
                    foreach (var app in appsToUpdate)
                    {
                        if (app.State == AppListState.Error)
                        {
                            Console.WriteLine($"Erro ao analisar {app.strName}({app.strID}) - {app.strVersion} --> {app.strNewVersion}");
                        }
                    }
                }
                await Functions.WaitEnterKeyUpTo(2000, "Nenhum update foi disparado...");
                return false;
            }

            int updated = await ProcessUpdatesAsync(config, appsToUpdate.Where(app => app.State == AppListState.Selected), ctsMain.Token).ConfigureAwait(false);
            await Functions.WaitEnterKeyUpTo(4000, $"{updated}/{selected} Atualizações realizadas");
            return updated > 0 && updated == selected;
        }
        catch (Exception ex)
        {
            ex.AppendToTimestampFile(_strLogPath, _EXCEPTION_FILE);

            ex.PrintAndWait("Erro ao atualizar...");
        }

        return false;
    }

    private static List<App> ProcessList(Config config)
    {
        List<App> appsFoundToUpdate = TextToAppList(config.sbResponse.ToString()) ?? [];

        if (appsFoundToUpdate.CountSafe() == 0)
        {
            return appsFoundToUpdate;
        }

        string strFormatID = $"{{0,-{appsFoundToUpdate.Max(found => found.strID.Length)}}} ";
        string strFormatVersion = $"{{0,-{appsFoundToUpdate.Max(found => found.strVersion.Length)}}}";
        int iMaxNewVersionLength = appsFoundToUpdate.Max(found => found.strNewVersion.Length);
        int column, line;

        appsFoundToUpdate.Sort((a, b) => string.Compare(a.strName, b.strName, StringComparison.OrdinalIgnoreCase));
        Span<App> spanApps = CollectionsMarshal.AsSpan(appsFoundToUpdate);
        for (int i = 0; i < spanApps.Length; i++)
        {
            (column, line) = Console.GetCursorPosition();
            var diffType = Functions.VersionDiffType(spanApps[i].strVersion, spanApps[i].strNewVersion);
            ref App item = ref spanApps[i];

            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Exception:
                    Functions.RevertLastWriteEx(column, line);
                    item.State = AppListState.Error;
                    continue;

                case Functions.VersionDiffTypeResult.Major or Functions.VersionDiffTypeResult.Simple:
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        item.State = AppListState.Available;
                        break;
                    }

                case Functions.VersionDiffTypeResult.Invalid:
                    Console.ForegroundColor = ConsoleColor.Red;
                    item.State = AppListState.Error;
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    item.State = AppListState.Available;
                    break;
            }

            Console.Write(strFormatID, appsFoundToUpdate[i].strID);
            Console.Write(strFormatVersion, appsFoundToUpdate[i].strVersion);
            Console.Write(" --> ");
            int rightPadding = iMaxNewVersionLength - appsFoundToUpdate[i].strNewVersion.Length;
            if (diffType is Functions.VersionDiffTypeResult.Exception or Functions.VersionDiffTypeResult.Invalid)
            {
                Console.Write(appsFoundToUpdate[i].strNewVersion.PadRight(rightPadding));
            }
            else
            {
                Functions.PrintNewVersion(appsFoundToUpdate[i].strVersion, appsFoundToUpdate[i].strNewVersion, rightPadding);
            }
            Console.ResetColor();
            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Simple:
                    Console.Write(" - disponível");
                    break;

                case Functions.VersionDiffTypeResult.Major:
                    Console.Write(" - Atenção*");
                    break;

                case Functions.VersionDiffTypeResult.NotAnalysed:
                    Console.Write(" - Por sua conta e Risco...");
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(" - Algo deu errado...");
                    break;
            }
            Console.WriteLine();
        }
        Console.ResetColor();

        int countAvailable = appsFoundToUpdate.Count(app => app.State == AppListState.Available);

        if (countAvailable == 0)
        {
            return appsFoundToUpdate;
        }

        Console.WriteLine();
        Console.WriteLine();
        spanApps = CollectionsMarshal.AsSpan(appsFoundToUpdate);
        for (int i = 0; i < spanApps.Length; i++)
        {
            if (spanApps[i].State != AppListState.Available)
            {
                continue;
            }
            (column, line) = Console.GetCursorPosition();
            Console.Write($"Atualizar {spanApps[i].strName}? [Y]es/[S]im - [I]gnore/I[g]norar  No/Não[Any Other]: ");

            var key = Console.ReadKey(true);
            Console.WriteLine();
            Functions.RevertLastWrite(column, line);
            if (key.Key is ConsoleKey.Y or ConsoleKey.S)
            {
                Console.WriteLine($"{spanApps[i].strName}({spanApps[i].strID}) adicionado");
                ref App item = ref spanApps[i];
                item.State = AppListState.Selected;
            }
            else if (key.Key is ConsoleKey.I or ConsoleKey.G)
            {
                ref App item = ref spanApps[i];
                item.State = AppListState.Ignored;
                ProcessAppIgnoreAsync(config, spanApps[i], CancellationToken.None).Wait();
            }
        }

        return appsFoundToUpdate;
    }

    private static async Task<int> ProcessUpdatesAsync(Config config, IEnumerable<App> appsToUpdate, CancellationToken ct = default)
    {
        if (!appsToUpdate.AnySafe())
        {
            return 0;
        }
        int iUpdates = 0;
        int column, line;
        foreach (var app in appsToUpdate)
        {
            config.sbResponse.Clear();
            config.sbResponse.Append("Output do update de ");
            config.sbResponse.AppendLine(app.strID);
            var ctsLoader = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                (column, line) = Console.GetCursorPosition();
                Console.Write("Atualizando ");
                Console.Write(app.strID);
                Console.Write(" ");

                try
                {
                    _ = Loader.Wait(config.LoaderSymbols, ctsLoader.Token);
                    await Cli.Wrap(config.WingetCommand).WithArguments([_STR_Update, app.strID, "--silent"], false)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => config.sbResponse.AppendLine(str)))
                        .ExecuteAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    Loader.Stop(ctsLoader);
                }

                Console.WriteLine();
                Functions.RevertLastWrite(column, line);
                Console.Write(app.strID);
                Console.WriteLine(" atualizado");
                iUpdates++;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.Write(app.strID);
                Console.Write(" EXCEPTION: ");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Check output log for more information");

                config.sbResponse.AppendLine();
                config.sbResponse.AppendLine(ex.ToString());
                config.sbResponse.AppendToTimestampFile(_strLogPath, $"{_UPD_EXCEPTION_FILE}_{app.strID}");
            }
        }

        return iUpdates;
    }

    private static async Task LogsMaintenanceAsync(string outputFilePrefix)
    {
        //scan for old logs
        var files = Directory.GetFiles(_strLogPath, $"{outputFilePrefix}_*.txt");
        if (files.Length > _QTY_LOGS)
        {
            //Sort files by Creation Date
            Array.Sort(files, (f1, f2) => File.GetCreationTime(f1).CompareTo(File.GetCreationTime(f2)));

            //Keep only last _QTY_LOGS files (and delete the rest)
            await Task.WhenAll(files[..^_QTY_LOGS].Select(async file => await Task.Run(() =>
            {
                try
                {
                    File.Delete(file);
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
                        _ = Loader.Wait_Old(ctsLoader.Token);
                        break;

                    case 2:
                        _ = Loader.Wait(loaderSymbols, ctsLoader.Token);
                        break;
                }
                Functions.WaitEnterKeyUpTo(600000).Wait();
            }
            catch
            {
                break;
            }
            finally
            {
                Loader.Stop(ctsLoader);
            }
        }
    }

    private static App ReadAppIdAndVersions(App appToAdd, ReadOnlySpan<char> spanFromId)
    {
        var spanSplit = spanFromId.Split(' ');

        int partCount = 0;
        foreach (var appPart in spanSplit)
        {
            if (appPart.End.Value - appPart.Start.Value <= 0)
            {
                continue;
            }

            if (partCount == 0)
            {
                appToAdd.strID = spanSplit.Source[appPart].ToString();
                partCount++;
                continue;
            }
            if (partCount == 1)
            {
                //Winget sometimes returns the current version with a "< A.B.C" and Available "A.B.C",
                // so need to join
                if (spanSplit.Source[appPart].Contains('<'))
                {
                    appToAdd.strVersion = $"{spanSplit.Source[appPart]} ";
                }
                else
                {
                    appToAdd.strVersion += $"{spanSplit.Source[appPart]}";
                    partCount++;
                }

                continue;
            }
            if (partCount >= 2)
            {
                appToAdd.strNewVersion = spanSplit.Source[appPart].ToString();
                break;
            }
        }

        return appToAdd;
    }

    private static List<App>? TextToAppList(string text)
    {
        if (text.IsEmpty())
        {
            return null;
        }
        bool bValid = false;

        var linesSpan = text.AsSpan().Split(Environment.NewLine);
        // Scan lines until starts with "Nome" and contains "ID"
        while(linesSpan.MoveNext())
        {
            if (ValidStartResponse(linesSpan.Source[linesSpan.Current]))
            {
                bValid = true;
                break;
            }
        }
        if(!bValid)
        {
            return null;
        }
        int indexID = linesSpan.Source[linesSpan.Current].IndexOf("ID");
        int IDSize = linesSpan.Source[linesSpan.Current].IndexOf("Vers") - indexID;
        // ---- Separator line
        linesSpan.MoveNext();

        List<App> apps = [];
        while (linesSpan.MoveNext())
        {
            var spanLine = linesSpan.Source[linesSpan.Current];
            if (spanLine.Length < indexID + IDSize || !spanLine.Contains(_STR_Winget, StringComparison.InvariantCulture))
            {
                break;
            }
            //When a application name is too long and there is any problem with encoding, sometimes it shifts from ID one or two front or back
            var spanName = spanLine[..(indexID - 1)].TrimEnd();
            while (spanName.Length > 0 && !char.IsBetween(spanName[^1], '!', '~'))
            {
                spanName = spanName[..^1];
            }
            if (spanName.Length == 0)
            {
                break;
            }

            int adjustedIndexID = indexID;
            while (adjustedIndexID < spanLine.Length - 1 && !char.IsBetween(spanName[^1], '!', '~'))
            {
                adjustedIndexID++;
            }
            if (adjustedIndexID > indexID + 4)
            {
                //Console.WriteLine($"Erro ao ler {spanName} ({lines[i]})");
                continue;
            }

            var appToAdd = new App
            {
                strName = spanName.ToString(),
                strID = "",
                strVersion = "",
                strNewVersion = ""
            };

            var spanFromId = spanLine[adjustedIndexID..];

            appToAdd = ReadAppIdAndVersions(appToAdd, spanFromId);

            apps.Add(appToAdd);
        }

        return apps;
    }

    private static bool ValidStartResponse(ReadOnlySpan<char> line)
    {
        return line.StartsWith("Nome") && line.Contains("ID", StringComparison.InvariantCulture);
    }

    private static void FilterResponse(ref StringBuilder sb, string line)
    {
        if (sb.Length > 0 || ValidStartResponse(line))
        {
            sb.AppendLine(line);
        }
    }

    private static async Task<bool> ProcessAppIgnoreAsync(Config config, App appToIgnore, CancellationToken ct = default)
    {
        try
        {
            var ctsLoader = new CancellationTokenSource();
            Console.WriteLine();
            var (column, line) = Console.GetCursorPosition();
            Console.Write("Ignorando ");
            Console.Write(appToIgnore.strID);
            Console.Write(" ");

            try
            {
                _ = Loader.Wait(config.LoaderSymbols, ctsLoader.Token);
                await Cli.Wrap(config.WingetCommand).WithArguments([_STR_Ignore, "--id",appToIgnore.strID, "--blocking"], false)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => config.sbResponse.AppendLine(str)))
                    .ExecuteAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                Loader.Stop(ctsLoader);
            }

            Console.WriteLine();
            Functions.RevertLastWrite(column, line);
            Console.Write(appToIgnore.strID);
            Console.WriteLine(" ignorado");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.Write(appToIgnore.strID);
            Console.Write(" EXCEPTION: ");
            Console.WriteLine(ex.Message);
            Console.WriteLine("Check output log for more information");

            config.sbResponse.AppendLine();
            config.sbResponse.AppendLine(ex.ToString());
            config.sbResponse.AppendToTimestampFile(_strLogPath, $"{_IGNORE_EXCEPTION_FILE}_{appToIgnore.strID}");
        }

        return true;
    }
}
