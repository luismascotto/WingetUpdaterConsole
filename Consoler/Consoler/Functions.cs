namespace Consoler;

public class Functions
{
    private static readonly string Spaces = "                                                                                                                                                                  ";

    public static void ClearCurrentConsoleLine(int extraLinesUp = 0)
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
        } while (extraLinesUp-- > 0);
    }

    public enum VersionDiffTypeResult
    {
        NotAnalysed = 0,
        Simple,
        Major,
        Invalid,
        Exception
    }

    private static readonly char[] VersionSeparators = ['-', '.'];
    private static ReadOnlySpan<char> Separators => VersionSeparators.AsSpan();

    public static VersionDiffTypeResult VersionDiffType(string actual, string updated)
    {
        try
        {
            if (actual.IsEmpty() || updated.IsEmpty() || actual.Contains('<') || updated.Contains('>'))
            {
                return VersionDiffTypeResult.NotAnalysed; // Do not compare versions
            }
            var mySpan = actual.AsSpan().SplitAny(Separators);
            var foundSpan = updated.AsSpan().SplitAny(Separators);
            int comp;
            while (true)
            {
                //Different lengths, foundVer part should be found greater before
                if (!mySpan.MoveNext() || !foundSpan.MoveNext())
                {
                    break;
                }
                // Same length, compare as string (some versions have letters, like 1.0.0-beta, or N-125365-g054dffd133-20260531)
                if (mySpan.Source[mySpan.Current].Length == foundSpan.Source[foundSpan.Current].Length)
                {
                    comp = foundSpan.Source[foundSpan.Current].CompareTo(mySpan.Source[mySpan.Current], StringComparison.InvariantCultureIgnoreCase);
                }
                else
                {
                    // 3 is higher than 10 (3.3.5 vs 3.10.0) in string comparison, but lower in
                    // natural comparison, so we need to parse as int and compare
                    comp = foundSpan.Source[foundSpan.Current].ParseNatural() - mySpan.Source[mySpan.Current].ParseNatural();
                }

                if (comp == 0)
                {
                    continue;
                }

                //We got a winner
                if (comp > 0)
                {
                    return foundSpan.Current.Start.Value == 0 ? VersionDiffTypeResult.Major : VersionDiffTypeResult.Simple;
                }
                //Here myVer part > foundVer or strange value
                break;
            }

            return VersionDiffTypeResult.Invalid;
        }
        catch (Exception ex)
        {
            ex.PrintAndWait($"##########      EXCEPTION ({actual} vs ({updated}))     ########");
            //Console.ForegroundColor = ConsoleColor.Red;
            //Console.WriteLine($"##########      EXCEPTION ({actual} vs ({found}))     ######## {ex.Message}");
            //Console.ResetColor();
            Thread.Sleep(100);
            return VersionDiffTypeResult.Exception;
        }
    }

    public static void PrintNewVersion(string actual, string updated, int disponivelPad, bool isIgnored)
    {
        int iCompVersion = 0;
        char separator = actual.Contains('-') ? '-' : '.';

        var currSemVer = actual.Split(separator);
        var nextSemVer = updated.Split(separator);

        while (iCompVersion < nextSemVer.Length)
        {
            if (iCompVersion >= currSemVer.Length)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
            }
            else if (currSemVer[iCompVersion] != nextSemVer[iCompVersion])
            {
                Console.ForegroundColor = isIgnored ? ConsoleColor.DarkYellow : ConsoleColor.Yellow;
            }
            Console.Write(nextSemVer[iCompVersion]);
            iCompVersion++;
            if (iCompVersion < nextSemVer.Length)
            {
                Console.Write(separator);
            }
        }

        if (disponivelPad > 0)
        {
            Console.Write(Spaces[..disponivelPad]);
        }
    }

    public static void RevertLastWrite(int previousCol, int previousLine)
    {
        int currentLine = Console.CursorTop;
        //int currentCol = Console.CursorLeft;

        while (currentLine >= previousLine)
        {
            if (currentLine == previousLine)
            {
                Console.SetCursorPosition(previousCol, currentLine);
            }
            else
            {
                Console.SetCursorPosition(0, currentLine);
            }
            Console.Write(Spaces[..Console.WindowWidth]);
            Console.SetCursorPosition(0, --currentLine);
        }

        Console.SetCursorPosition(previousCol, previousLine);
    }

    public static void RevertLastWriteEx(int previousCol, int previousLine)
    {
        int col, lin, moves = 0;
        while (((col, lin) = Console.GetCursorPosition()) != (previousCol, previousLine))
        {
            if (Console.CursorTop <= previousLine && Console.CursorLeft <= previousCol)
            {
                break;
            }
            Console.Write("\b ");
            if (++moves % 5 == 0)
            {
                Thread.Sleep(2);
            }
            //Check safety
            col--;
            if (col < 0)
            {
                col = Console.WindowWidth - 1;
                lin--;
            }
            Console.SetCursorPosition(col, lin);
        }
    }

    public static async Task WaitEnterKeyUpTo(int timeoutMilliseconds, string message = "")
    {
        if (message.HasSomething())
        {
            Console.WriteLine(message);
        }
        await Task.WhenAny([Task.Delay(timeoutMilliseconds, CancellationToken.None), Task.Run(PressEnter)])
           .ConfigureAwait(false);
    }

    public static void PressEnter()
    {
        PressAKey(ConsoleKey.Enter);
    }

    /**
     * Wait for a specific key press, or any key if ConsoleKey.None is provided
     * @param key The key to wait for
     */

    public static void PressAKey(ConsoleKey key = ConsoleKey.None)
    {
        do
        {
            var keyRead = Console.ReadKey(true);
            if (key == ConsoleKey.None || keyRead.Key == key)
            {
                break;
            }
            //await Task.Delay(100, CancellationToken.None);
        } while (true);
    }

    public static void WriteLineWrapIndented(string? text, int indentSpaces = 0)
    {
        if (!text.HasSomething())
        {
            return;
        }
        int realWidth = Console.WindowWidth - indentSpaces;
        var spanText = text.AsSpan();
        while (spanText.Length > 0)
        {
            if (spanText.Length <= realWidth)
            {
                realWidth = spanText.Length;
            }
            WriteSpaces(indentSpaces);
            //if (indentSpaces > 0)
            //{
            //    Console.Write(spaces);
            //}
            Console.WriteLine(spanText[..realWidth]);
            spanText = spanText[realWidth..];
        }
    }

    public static void WriteSpaces(int count)
    {
        if (count <= 0)
        {
            return;
        }
        Console.Write(Spaces.AsSpan()[..count]);
    }

    public static string GenerateOutputFilename(string path, string prefix, string extension)
    {
        return $"{path}{prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{DateTime.Now.Ticks:X16}.{extension}";
    }

    public static string GetResponseFilePath(string responseFile)
    {
        if (Path.IsPathFullyQualified(responseFile))
        {
            return responseFile;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, responseFile);
    }
}
