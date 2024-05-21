using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Consoler;

public class Functions
{
    private static string SpacesWindowWidth = new(' ', Console.WindowWidth);
    //Write a function that writes dots on console until receives a cancellation token
    public static async Task WriteDotsAsync(CancellationToken cancellationToken)
    {
        var rnd = Random.Shared;
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write(".");
            await Task.Delay(rnd!.Next(50, 500), cancellationToken);
        }
    }
    public static async Task WriteLoader(CancellationToken cancellationToken)
    {
        var ch = new[] { '|', '/', '-', '\\' };
        int i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write(ch[i % ch.Length]);

            await Task.Delay(250, cancellationToken);
            Console.Write("\b");
            i++;
        }
    }

    public static string GetWindows1252fromUtf8(string utf8)
    {
        var srcEncoding = Encoding.UTF8; // utf-8
        var destEncoding = Encoding.Default;
        //var destEncoding = Encoding.GetEncoding(Encoding.Latin1); // windows-1252

        // convert the source bytes to the destination bytes
        var destBytes = Encoding.Convert(srcEncoding, destEncoding, srcEncoding.GetBytes(utf8));
        var destString = destEncoding.GetString(destBytes);

        return destString;
    }
    public static string GetUTF8(string text)
    {
        Encoding utf8 = Encoding.GetEncoding("UTF-8");
        Encoding latin = Encoding.Latin1;

        byte[] win1251Bytes = latin.GetBytes(text);
        byte[] utf8Bytes = Encoding.Convert(latin, utf8, win1251Bytes);

        return utf8.GetString(win1251Bytes);
    }

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
            Console.Write(SpacesWindowWidth);
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


}


