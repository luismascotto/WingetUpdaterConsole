using System.Text;

namespace Consoler;

public class Functions
{
    static readonly string SpacesWindowWidth = new(' ', Console.WindowWidth);
    static readonly char[] LoaderChars = ['|', '/', '-', '\\'];
    //Write a function that writes dots on console until receives a cancellation token
    public static async Task WriteDotsAsync(CancellationToken cancellationToken)
    {
        var rnd = Random.Shared;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Write(".");
                await Task.Delay(rnd!.Next(50, 500), cancellationToken);
            }
        }
        finally
        {
            Console.WriteLine();
        }
    }

    private static int i = 0;
    private static int waitMs = maxWaitMs;
    private static bool increase = false;
    private static bool backwards = false;

    private const int minWaitMs = 50;
    private const int maxWaitMs = 200;

    private const int incrementMs = 20;

    private const int decrementHighMs = 10;
    private const int decrementMidMs = 10;
    private const int decrementLowMs = 10;
    private const int decrementMs = 2;

    private const int rangeSteps = 6;
    private const int stepMs = maxWaitMs / rangeSteps;

    private const int randomCount = 5;

    private const int loaderSlots = 5;


    private static void checkLoader()
    {
        if (waitMs < minWaitMs || waitMs > maxWaitMs || i < 0)
        {
            waitMs = maxWaitMs;
            increase = false;
            backwards = false;
            i = 0;
        }
    }

    public static async Task WriteLoader(CancellationToken cancellationToken)
    {
        checkLoader();
        int countRandom = 0;
        int currCol = Console.CursorLeft;
        int acumulatedWaitMs = 0;
        int countForRandomPosition = 0;
        char[] loaderPositions = new char[loaderSlots];
        bool canCheckJackpot = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (acumulatedWaitMs >= 1000)
            {
                acumulatedWaitMs -= 1000;
                countForRandomPosition++;
                if (countForRandomPosition > loaderSlots - 1)
                {
                    Console.SetCursorPosition(Random.Shared.Next(currCol, currCol + loaderSlots), Console.CursorTop);
                    canCheckJackpot = true;
                }
                else
                {
                    Console.SetCursorPosition(currCol + countForRandomPosition, Console.CursorTop);
                }
            }
            acumulatedWaitMs += waitMs;
            if (countRandom > 0)
            {
                countRandom--;
                i = Random.Shared.Next(0, LoaderChars.Length);
            }
            char currLoaderChar = LoaderChars[i++ % LoaderChars.Length];
            loaderPositions[Console.CursorLeft - currCol] = currLoaderChar;
            Console.Write(currLoaderChar);
            if (canCheckJackpot)
            {
                canCheckJackpot = !checkJackpot(loaderPositions, loaderSlots);
            }
            if (backwards)
            {
                i += LoaderChars.Length - 2;
            }
            try
            {
                await Task.Delay(waitMs, cancellationToken);
                if (countRandom > 0)
                {
                    continue;
                }
                if (increase)
                {
                    waitMs += incrementMs;
                    if (waitMs > maxWaitMs)
                    {
                        waitMs = maxWaitMs;
                        increase = false;
                        countRandom = randomCount;
                        backwards = !backwards;
                    }
                    continue;
                }
                if (waitMs > 5 * stepMs)
                {
                    waitMs -= decrementHighMs;
                }
                if (waitMs > 4 * stepMs)
                {
                    waitMs -= decrementMidMs;
                }
                if (waitMs > 3 * stepMs)
                {
                    waitMs -= decrementLowMs;
                }
                if (waitMs > 2 * stepMs)
                {
                    waitMs -= decrementMs;
                }
                waitMs -= 2;
                if (waitMs < minWaitMs)
                {
                    waitMs = minWaitMs;
                    increase = true;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
                await Task.Delay(1000, CancellationToken.None);
            }
            finally
            {
                Console.Write("\b");
            }
        }
        Console.ResetColor();
    }

    private static bool checkJackpot(char[] loaderPositions, int positions)
    {
        for (int j = 0; j < positions - 1; j++)
        {
            if (loaderPositions[j] != loaderPositions[j + 1])
            {
                return false;
            }
        }
        //Jackpot!
        if (Console.ForegroundColor != ConsoleColor.Green)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.BackgroundColor = ConsoleColor.Black;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Green;
        }
        return true;
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

    public static async Task WaitEnterKeyUpTo(int timeoutMilliseconds)
    {
        await Task.WhenAny([Task.Delay(timeoutMilliseconds, CancellationToken.None), Task.Run(Console.ReadKey)])
           .ConfigureAwait(false);
    }
}


