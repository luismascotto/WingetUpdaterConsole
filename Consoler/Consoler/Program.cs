using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using Microsoft.Extensions.Configuration;

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

    struct App
    {
        public string strName;
        public string strID;
        public string strVersion;
        public string strNewVersion;
        public AppListState State;
    }

    struct Config
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
    private const string _OUTPUT_FILE = "winget_output";

    private const string _ignoredAppsFilename = "ignored.json";
    const string STR_Winget = "winget";
    const string STR_Update = "upgrade";
    const string STR_FakeWinget = "FakeWinget";

    private static readonly string _strLogPath = Path.Combine($"{Path.GetDirectoryName(Environment.ProcessPath)}", "Logs");

    static readonly JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static async Task Main(string[] args)
    {
        var switchMappings = new Dictionary<string, string>()
           {
               { "--debug", "DebugLoader" },
               { "--loader", "LoaderType" },
               { "--symbols", "LoaderSymbols" },
               { "--response-file", "ResponseFile" },
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
            WingetCommand = STR_Winget
        };


        Directory.CreateDirectory(_strLogPath);


        if (config.ResponseFile.HasSomething())
        {
            if (!File.Exists(config.ResponseFile))
            {

                throw new FileNotFoundException($"Response file not found: {config.ResponseFile}");
            }
            config.WingetCommand = STR_FakeWinget;

        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        Console.Title = "winget-upgrade";

        if (config.DebugLoader)
        {
            Console.WriteLine($"\t\tWINGET (Debug Loader)");

            ProcessDebugLoader(config.LoaderType, config.LoaderSymbols);
            return;
        }

        uint safeGuard = 0;
        while (true)
        {
            bool flowControl = await ExecuteUpgradeCheckAsync(config).ConfigureAwait(false);
            if (!flowControl || ++safeGuard > _QTY_RUNS)
            {
                return;
            }
        }
    }

    private static async Task<bool> ExecuteUpgradeCheckAsync(Config config)
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
                    await Cli.Wrap(config.WingetCommand).WithArguments([STR_Update], false)
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
                await Functions.WaitEnterKeyUpTo(1000, "Nenhum update retornado...");
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
            int ignored = appsToUpdate.Count(app => app.State == AppListState.Ignored);
            int available = appsToUpdate.Count(app => app.State == AppListState.Available);
            int selected = appsToUpdate.Count(app => app.State == AppListState.Selected);

            if (selected == 0)
            {
                if(available == 0)
                {
                    foreach (var app in appsToUpdate)
                    {
                        if (app.State == AppListState.Ignored)
                        {
                            Console.WriteLine($"{app.strName}({app.strID}) - {app.strVersion} --> {app.strNewVersion}: {nameof(app.State)}");
                        }
                        if (app.State == AppListState.Error)
                        {
                            Console.WriteLine($"Erro ao analisar {app.strName}({app.strID}) - {app.strVersion} --> {app.strNewVersion}");
                        }
                    }
                }
                await Functions.WaitEnterKeyUpTo(2000, "Nenhum update foi disparado...");
                return false;
            }


            int updated = await ProcessUpdatesAsync(config, appsToUpdate, ctsMain.Token).ConfigureAwait(false);
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
        List<App> appsFoundToUpdate = [];
        TextToApp(config.sbResponse.ToString(), ref appsFoundToUpdate);

        if (appsFoundToUpdate.Count == 0)
        {
            return appsFoundToUpdate;
        }
        List<App> appsToIgnore = LoadAppsIgnoredAsync(CancellationToken.None).Result;

        string strFormatID = $"{{0,-{appsFoundToUpdate.Max(found => found.strID.Length)}}} ";
        string strFormatVersion = $"{{0,-{appsFoundToUpdate.Max(found => found.strVersion.Length)}}}";
        int column, line;

        appsFoundToUpdate.Sort((a, b) => string.Compare(a.strName, b.strName, StringComparison.OrdinalIgnoreCase));
        Span<App> spanApps = CollectionsMarshal.AsSpan(appsFoundToUpdate);
        for (int i = 0; i < spanApps.Length; i++)
        {
            (column, line) = Console.GetCursorPosition();
            var diffType = Functions.VersionDiffType(spanApps[i].strVersion, spanApps[i].strNewVersion);
            bool isIgnored = false;
            ref App item = ref spanApps[i];

            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Exception:
                    Functions.RevertLastWriteEx(column, line);
                    item.State = AppListState.Error;
                    continue;
                case Functions.VersionDiffTypeResult.Major or Functions.VersionDiffTypeResult.Simple:
                    {
                        foreach (var app in appsToIgnore)
                        {
                            if (app.strID == spanApps[i].strID && app.strVersion == spanApps[i].strVersion && app.strNewVersion == spanApps[i].strNewVersion)
                            {
                                isIgnored = true;
                                break;
                            }
                        }

                        if (isIgnored)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            item.State = AppListState.Ignored;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            item.State = AppListState.Available;
                        }

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
            int rightPadding = appsFoundToUpdate.Max(found => found.strNewVersion.Length) - appsFoundToUpdate[i].strNewVersion.Length;
            if (diffType is Functions.VersionDiffTypeResult.Exception or Functions.VersionDiffTypeResult.Invalid)
            {
                Console.Write(appsFoundToUpdate[i].strNewVersion.PadRight(rightPadding));
            }
            else
            {
                Functions.PrintNewVersion(appsFoundToUpdate[i].strVersion, appsFoundToUpdate[i].strNewVersion, rightPadding, isIgnored);
            }
            Console.ResetColor();
            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Simple:
                    {
                        Console.Write(" - disponível");
                        if (isIgnored)
                        {
                            Console.Write(" PORÉM ignorado...");
                        }
                    }
                    break;

                case Functions.VersionDiffTypeResult.Major:
                    {
                        Console.Write(" - Atenção*");
                    }
                    break;

                case Functions.VersionDiffTypeResult.NotAnalysed:
                    {
                        Console.Write(" - Por sua conta e Risco...");
                    }
                    break;

                default:
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(" - Algo deu errado...");
                    }
                    break;

            }
            Console.WriteLine();
        }
        Console.ResetColor();

        int countAvailable = appsFoundToUpdate.Count(app => app.State == AppListState.Available);
        //appsFoundToUpdate.RemoveAll(app => app.intState != AppListState.Available);

        if (countAvailable == 0)
        {
            return appsFoundToUpdate;
        }

        Console.WriteLine();
        Console.WriteLine();
        spanApps = CollectionsMarshal.AsSpan(appsFoundToUpdate);
        for (int i = 0; i < spanApps.Length; i++)
        {
            if(spanApps[i].State != AppListState.Available)
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
                appsToIgnore.Add(spanApps[i]);
                SaveAppsIgnoredAsync(appsToIgnore, CancellationToken.None).Wait(CancellationToken.None);
            }
        }

        return appsFoundToUpdate;
    }

    private static async Task<int> ProcessUpdatesAsync(Config config, List<App> appsToUpdate, CancellationToken ct = default)
    {
        if (!appsToUpdate.AnySafe())
        {
            return 0;
        }
        int iUpdates = 0;
        var sbResponseUpd = new StringBuilder();
        foreach (var app in appsToUpdate)
        {
            sbResponseUpd.Clear();
            sbResponseUpd.Append("Output do update de ");
            sbResponseUpd.AppendLine(app.strID);
            var ctsLoader = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                Console.Write("Atualizando ");
                Console.Write(app.strID);
                Console.Write(" ");

                try
                {
                    _ = Loader.Wait(config.LoaderSymbols, ctsLoader.Token);
                    await Cli.Wrap(config.WingetCommand).WithArguments([STR_Update, app.strID, "--silent"], false)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => sbResponseUpd.AppendLine(str)))
                        .ExecuteAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    Loader.Stop(ctsLoader);
                }

                Console.WriteLine();
                Console.Write(app.strID);
                Console.WriteLine(" atualizado");
                iUpdates++;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.Write(app.strID);
                Console.Write(" EXCEPTION: ");
                Console.Write(ex.Message);
                Console.WriteLine();
                Console.Write("Check output log for more information");

                sbResponseUpd.AppendLine();
                sbResponseUpd.AppendLine(ex.ToString());
                sbResponseUpd.AppendToTimestampFile(_strLogPath, $"{_UPD_EXCEPTION_FILE}_{app.strID}");

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

    private static async Task SaveAppsIgnoredAsync(List<App> apps, CancellationToken ct = default)
    {
        try
        {
            await File.WriteAllTextAsync(_ignoredAppsFilename, JsonSerializer.Serialize(apps, jsonSerializerOptions), ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar apps ignorados: {ex.Message}");
        }
    }

    private static async Task<List<App>> LoadAppsIgnoredAsync(CancellationToken ct = default)
    {
        List<App> appsToIgnore = [];
        if (File.Exists(_ignoredAppsFilename))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_ignoredAppsFilename, ct).ConfigureAwait(false);
                appsToIgnore = JsonSerializer.Deserialize<List<App>>(json, jsonSerializerOptions) ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar apps ignorados: {ex.Message}");
            }
        }
        return appsToIgnore;
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

    private static bool TextToApp(string text, ref List<App> apps)
    {
        if (text.IsEmpty() || apps is null)
        {
            return false;
        }
        // Scan lines until starts with "Nome" and contains "ID"
        var lines = text.Split(Environment.NewLine);
        int startLineIndex = Array.FindIndex(lines, ValidStartResponse);
        if (startLineIndex == -1)
        {
            return false;
        }
        int indexID = lines[startLineIndex].IndexOf("ID");
        int IDSize = lines[startLineIndex].IndexOf("Vers") - indexID;



        for (int i = startLineIndex + 2; i < lines.Length && lines[i]?.Length >= indexID + IDSize && lines[i].Contains(STR_Winget); i++)
        {
            //When a application name is too long and there is any problem with encoding, sometimes it shifts from ID one or two front or back
            var spanLine = lines[i].AsSpan();
            var spanName = spanLine[..(indexID - 1)].TrimEnd();
            while (spanName.Length > 0 && !char.IsBetween(spanName[^1], '!', '~'))
            {
                spanName = spanName[..^1];
            }
            if (spanName.Length == 0)
            {
                return false;
            }

            int adjustedIndexID = indexID;
            while (adjustedIndexID < lines[i].Length - 1 && !char.IsBetween(spanName[^1], '!', '~'))
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
        return apps.Count > 0;

    }

    private static bool ValidStartResponse(string line)
    {
        return line.StartsWith("Nome") && line.Contains("ID");
    }

    private static void FilterResponse(ref StringBuilder sb, string line)
    {
        if (sb.Length > 0 || ValidStartResponse(line))
        {
            sb.AppendLine(line);
        }
    }


}

