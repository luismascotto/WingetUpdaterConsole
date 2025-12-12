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
    public static int VersionComparer(string actual, string found)
    {
        try
        {
            if (actual.Contains('<') || found.Contains('>'))
            {
                return 0; // Do not compare versions
            }
            var my = actual.Split('.');
            var other = found.Split('.');

            int iMinLen = Math.Min(my.Length, other.Length);

            for (int i = 0; i < iMinLen; i++)
            {
                if (int.Parse(my[i]) > int.Parse(other[i]))
                {
                    return 1;
                }
                if (int.Parse(my[i]) < int.Parse(other[i]))
                {
                    return -1;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"##########      EXCEPTION ({actual} vs ({found}))     ######## {ex.Message}");
            Console.ResetColor();
            Thread.Sleep(100);
            return 2;
        }
    }

    public static void RevertLastWrite(int previousCol, int previousLine)
    {
        int currentLine = Console.CursorTop;
        int currentCol = Console.CursorLeft;

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

    public static async Task WaitEnterKeyUpTo(int timeoutMilliseconds)
    {
        _ = await Task.WhenAny([Task.Delay(timeoutMilliseconds, CancellationToken.None), Task.Run(PressEnter)])
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
        string spaces = new(' ', indentSpaces);
        int realWidth = Console.WindowWidth - indentSpaces;
        var spanText = text.AsSpan();
        while (spanText.Length > 0)
        {
            if(spanText.Length <= realWidth)
            {
                realWidth = spanText.Length;
            }
            if (indentSpaces > 0)
            {
                Console.Write(spaces);
            }
            Console.WriteLine(spanText[..realWidth].ToString());
            spanText = spanText[realWidth..];
        }
    }
}


