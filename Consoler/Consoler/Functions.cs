using System.Text;

namespace Consoler;

public class Functions
{
    static readonly string Spaces = "                                                                                                                                                                  ";

    // public static string GetWindows1252fromUtf8(string utf8)
    // {
    //     var srcEncoding = Encoding.UTF8; // utf-8
    //     var destEncoding = Encoding.Default;
    //     //var destEncoding = Encoding.GetEncoding(Encoding.Latin1); // windows-1252

    //     // convert the source bytes to the destination bytes
    //     var destBytes = Encoding.Convert(srcEncoding, destEncoding, srcEncoding.GetBytes(utf8));
    //     var destString = destEncoding.GetString(destBytes);

    //     return destString;
    // }
    // public static string GetUTF8(string text)
    // {
    //     Encoding utf8 = Encoding.GetEncoding("UTF-8");
    //     Encoding latin = Encoding.Latin1;

    //     byte[] win1251Bytes = latin.GetBytes(text);
    //     byte[] utf8Bytes = Encoding.Convert(latin, utf8, win1251Bytes);

    //     return utf8.GetString(win1251Bytes);
    // }

    public static void ClearCurrentConsoleLine(int extraLinesUp = 0)
    {
        int currentLine = Console.CursorTop;
        int desiredLine = Console.CursorTop - extraLinesUp;
        if (desiredLine <= 0)
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            return;
        }
        do
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, --currentLine);
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


    public static VersionDiffTypeResult VersionDiffType(string actual, string updated)
    {
        try
        {
            if (actual.IsEmpty() || updated.IsEmpty() || actual.Contains('<') || updated.Contains('>'))
            {
                return VersionDiffTypeResult.NotAnalysed; // Do not compare versions
            }

            char separator = actual.Contains('-') ? '-' : '.';

            var mySpan = actual.AsSpan().Split(separator);
            var foundSpan = updated.AsSpan().Split(separator);


            int myVer = -1;
            int foundVer = -1;
            while (true)
            {
                if (mySpan.MoveNext())
                {
                    myVer = mySpan.Source[mySpan.Current].ParseNatural();
                }
                if (foundSpan.MoveNext())
                {
                    foundVer = foundSpan.Source[foundSpan.Current].ParseNatural();
                }
                //Here if both are 0, we reached the end of both versions, they are invalid
                if (myVer == -1 && foundVer == -1)
                {
                    break;
                }
                //Here if both are equal, go to next part
                if (myVer == foundVer)
                {
                    continue;
                }

                //We got a winner
                if (foundVer > myVer)
                {
                    return foundSpan.Current.Start.Value == 0 ? VersionDiffTypeResult.Major : VersionDiffTypeResult.Simple;
                }
                //Here myVer > foundVer
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
        if(message.HasSomething())
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


