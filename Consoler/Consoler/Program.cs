using CliWrap;
using Microsoft.Extensions.Configuration;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private const string _EXCEPTION_FILE = "exception";
    private const string _UPD_EXCEPTION_FILE = "update";
    private const string _OUTPUT_FILE = "winget_output";


    private const string _ignoredAppsFilename = "ignored.json";
    const string STR_Winget = "winget";
    const string STR_Update = "upgrade";

    private static readonly string _strPath = $"{Path.GetDirectoryName(Environment.ProcessPath)}";

    private static bool _DebugLoader;
    private static int _LoaderType;
    private static string? _LoaderSymbols;
    private static string? _ResponseFile;

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

        _ResponseFile = config["ResponseFile"] ?? "";
        if (_ResponseFile.HasSomething() && !File.Exists(_ResponseFile))
        {
            _ResponseFile = Functions.GetResponseFilePath(_ResponseFile);

            if (!File.Exists(_ResponseFile))
            {
                throw new FileNotFoundException($"Response file not found: {_ResponseFile}");
            }
        }

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
        Console.WriteLine("\t\tWINGET");
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
            if (_DebugLoader)
            {
                await Functions.WaitEnterKeyUpTo(600000);
                Loader.Stop(ctsLoader);
                return;
            }
            var sbOutput = new StringBuilder();
            if (_ResponseFile.HasSomething())
            {
                sbOutput.Append(File.ReadAllText(_ResponseFile));
            }
            else
            {
                await Cli.Wrap(STR_Winget).WithArguments(STR_Update)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => sbOutput.AppendLine(str)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync().ConfigureAwait(false);
            }
            Loader.Stop(ctsLoader);
            if (sbOutput.Length == 0)
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine();
            if (_ResponseFile.IsEmpty())
            {
                sbOutput.AppendToTimestampFile(_strPath, _OUTPUT_FILE);
                await LogsMaintenanceAsync(_OUTPUT_FILE).ConfigureAwait(false);
            }

            await ProcessListAsync(sbOutput).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            Loader.Stop(ctsLoader);
            ex.AppendToTimestampFile(_strPath, "main");

            ex.PrintAndWait("Erro ao atualizar...");
        }
    }

    private static async Task ProcessListAsync(StringBuilder sbInput, CancellationToken ct = default)
    {
        // Scan lines until starts with "Name" and contains "winget"
        var lines = sbInput.ToString().Split(Environment.NewLine);
        int startLineIndex = Array.FindIndex(lines, line => line.StartsWith("Nome") && line.Contains("ID"));
        if (startLineIndex == -1)
        {
            await Functions.WaitEnterKeyUpTo(2000, "Nenhum app encontrado para atualizar");
            return;
        }
        int indexID = lines[startLineIndex].IndexOf("ID");
        int IDSize = lines[startLineIndex].IndexOf("Vers") - indexID;
        //int indexVersao = lines[0].IndexOf("Vers");
        //int VersaoSize = lines[0].IndexOf("Dispon") - indexVersao;



        List<App> appsFoundToUpdate = [];
        int namePaddingLength = 18;
        int idPaddingLength = 10;
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
                Console.WriteLine($"Erro ao ler ({lines[i]})");
                continue;
            }

            int adjustedIndexID = indexID;
            while (adjustedIndexID < lines[i].Length - 1 && !char.IsBetween(spanName[^1], '!', '~'))
            {
                adjustedIndexID++;
            }
            if (adjustedIndexID > indexID + 4)
            {
                Console.WriteLine($"Erro ao ler {spanName} ({lines[i]})");
                continue;
            }

            var appToAdd = new App
            {
                Nome = spanName.ToString(),
                ID = "",
                Versao = "",
                Disponivel = ""
            };

            var spanFromId = spanLine[adjustedIndexID..];

            appToAdd = ReadAppIdAndVersions(appToAdd, spanFromId);

            appsFoundToUpdate.Add(appToAdd);

            //Padding
            if (appToAdd.Nome.Length > namePaddingLength)
            {
                namePaddingLength = appToAdd.Nome.Length;
            }

            if (appToAdd.ID.Length > idPaddingLength)
            {
                idPaddingLength = appToAdd.ID.Length;
            }
        }

        if (appsFoundToUpdate.Count == 0)
        {
            Console.WriteLine("Nenhum app encontrado para atualizar");
            await Functions.WaitEnterKeyUpTo(2000);
            return;
        }
        List<App> appsToIgnore = await LoadAppsIgnoredAsync(ct).ConfigureAwait(false);

        List<App> appsAvailable = [];
        string nameFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Nome.Length)}}}";
        string idFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.ID.Length)}}} ";
        string curVerFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Versao.Length)}}}";
        //string avlbVerFmt = $"{{0,-{appsFoundToUpdate.Max(found => found.Disponivel.Length)}}}";
        appsFoundToUpdate.Sort((a, b) => string.Compare(a.Nome, b.Nome, StringComparison.OrdinalIgnoreCase));
        foreach (var app in appsFoundToUpdate)
        {
            var (column, line) = Console.GetCursorPosition();
            var diffType = Functions.VersionDiffType(app.Versao, app.Disponivel);
            bool isIgnored = false;
            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Exception:
                    Functions.RevertLastWriteEx(column, line);
                    continue;
                case Functions.VersionDiffTypeResult.Major or Functions.VersionDiffTypeResult.Simple:
                    {
                        isIgnored = appsToIgnore.Any(a => a.ID == app.ID && a.Versao == app.Versao && a.Disponivel == app.Disponivel);
                        if (isIgnored)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            appsAvailable.Add(app);
                        }

                        break;
                    }

                case Functions.VersionDiffTypeResult.Invalid:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    appsAvailable.Add(app);
                    break;
            }

            Console.Write(idFmt, app.ID);
            //Console.Write(" ");
            Console.Write(curVerFmt, app.Versao);
            Console.Write(" --> ");
            int rightPadding = appsFoundToUpdate.Max(found => found.Disponivel.Length) - app.Disponivel.Length;
            if (diffType is Functions.VersionDiffTypeResult.Exception or Functions.VersionDiffTypeResult.Invalid)
            {
                Console.Write(app.Disponivel.PadRight(rightPadding));
            }
            else
            {
                Functions.PrintNewVersion(app.Versao, app.Disponivel, rightPadding, isIgnored);
            }
            //Console.Write(avlbVerFmt, app.Disponivel);
            Console.ResetColor();
            switch (diffType)
            {
                case Functions.VersionDiffTypeResult.Simple:
                    {
                        Console.Write($" - disponível");
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
        appsFoundToUpdate.Shrink();

        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();

        List<string> appsToUpdate = [];
        var countNewIgnores = 0;
        foreach (var app in appsAvailable)
        {
            var (column, line) = Console.GetCursorPosition();
            Console.Write($"Atualizar {app.Nome}? [Y]es/[S]im - [I]gnore/I[g]norar  No/Não[Any Other]: ");

            var key = Console.ReadKey(true);
            Console.WriteLine();
            Functions.RevertLastWrite(column, line);
            if (key.Key is ConsoleKey.Y or ConsoleKey.S)
            {
                Console.WriteLine($"{app.Nome}({app.ID}) adicionado");
                appsToUpdate.Add(app.ID);
            }
            else if (key.Key is ConsoleKey.I or ConsoleKey.G)
            {
                appsToIgnore.Add(app);
                countNewIgnores++;
            }
        }
        if (countNewIgnores > 0)
        {
            await SaveAppsIgnoredAsync(appsToIgnore, ct).ConfigureAwait(false);
        }
        appsToIgnore.Shrink();

        if (appsToUpdate.Count == 0)
        {
            await Functions.WaitEnterKeyUpTo(1000, "Nenhum update para realizar...");
            return;
        }
        int iUpdates = await ProcessUpdatesAsync(appsToUpdate, ct).ConfigureAwait(false);
        if (iUpdates == 0)
        {
            await Functions.WaitEnterKeyUpTo(2000, "Nenhum update foi realizado");
            return;
        }
        await Functions.WaitEnterKeyUpTo(4000, $"{iUpdates} atualizaç{(iUpdates == 1 ? "ão" : "ões")} realizada{(iUpdates == 1 ? "" : "s")}");

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
                appToAdd.ID = spanSplit.Source[appPart].ToString();
                partCount++;
                continue;
            }
            if (partCount == 1)
            {
                //Winget sometimes returns the current version with a "< A.B.C" and Available "A.B.C", 
                // so need to join
                if (spanSplit.Source[appPart].Contains('<'))
                {
                    appToAdd.Versao = $"{spanSplit.Source[appPart]} ";
                }
                else
                {
                    appToAdd.Versao += $"{spanSplit.Source[appPart]}";
                    partCount++;
                }

                continue;
            }
            if (partCount >= 2)
            {
                appToAdd.Disponivel = spanSplit.Source[appPart].ToString();
                break;
            }
        }

        return appToAdd;
    }

    private static async Task<int> ProcessUpdatesAsync(List<string> appsToUpdate, CancellationToken ct = default)
    {
        if (!appsToUpdate.AnySafe())
        {
            return 0;
        }
        int iUpdates = 0;
        var strBuilder = new StringBuilder();
        foreach (string appID in appsToUpdate)
        {
            strBuilder.Clear();
            strBuilder.Append("Output do update de ");
            strBuilder.AppendLine(appID);
            var ctsLoader = new CancellationTokenSource();
            try
            {
                Console.WriteLine();
                Console.Write("Atualizando ");
                Console.Write(appID);
                Console.Write(" ");

                switch (_LoaderType)
                {
                    case 1:
                        _ = Loader.Wait(ctsLoader!.Token);
                        break;
                    case 2:
                        _ = Loader.Wait2(_LoaderSymbols!, ctsLoader!.Token);
                        break;
                }
                await Cli.Wrap(STR_Winget).WithArguments([STR_Update, appID, "--silent"])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate((str) => strBuilder.AppendLine(str)))
                    .ExecuteAsync(ct).ConfigureAwait(false);
                Loader.Stop(ctsLoader);

                Console.WriteLine();
                Console.Write(appID);
                Console.WriteLine(" atualizado");
                iUpdates++;
            }
            catch (Exception ex)
            {
                Loader.Stop(ctsLoader);

                strBuilder.AppendLine();
                strBuilder.AppendLine(ex.ToString());
                strBuilder.AppendToTimestampFile(_strPath, $"{_UPD_EXCEPTION_FILE}_{appID}");


                await Functions.WaitEnterKeyUpTo(30000, $"{Environment.NewLine}{appID} EXCEPTION: {ex.Message}{Environment.NewLine}Check output log for more information");
            }
        }

        return iUpdates;
    }

    private static async Task LogsMaintenanceAsync(string outputFilePrefix)
    {
        //scan for old logs
        var files = Directory.GetFiles(_strPath!, $"{outputFilePrefix}_*.txt");
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
}

