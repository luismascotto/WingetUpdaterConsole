using System.Diagnostics;
using System.Text;

namespace Consoler;

public class Loader
{
    private static char[] LoaderChars = [];
    //static readonly char[] LoaderChars = ['|', '/', '-', '\\'];
    //private const string defaultSymbols = "-\\|/";
    static readonly string[] defaultSymbolsList = ["-\\|/", "+=-*", "mMwW", "0OoC", ".;:|", "({[<", ")}]>"];

    private static int i = 0;
    private static int waitMs = maxWaitMs;
    private static bool increase = false;
    private static bool backwards = false;
    private static int loaderPosition = 0;

    private const int minWaitMs = 50;
    private const int maxWaitMs = 200;

    //private const int minWaitMs = 120;
    //private const int maxWaitMs = 250;

    private const int incrementMs = 20;

    private const int decrementHighMs = 10;
    private const int decrementMidMs = 10;
    private const int decrementLowMs = 10;
    private const int decrementMs = 2;

    private const int rangeSteps = 6;
    private const int stepMs = maxWaitMs / rangeSteps;

    private const int randomCount = 5;

    private const int loaderSlots = 5;

    private const int waitMsChangeLoaderPosition = 1000;
    readonly private static char[] loaderPositions = new char[loaderSlots];

    private static ConsoleColor LastForegroundColor = ConsoleColor.White;
    private static ConsoleColor LastBackgroundColor = ConsoleColor.Black;

    private static void CheckLoader()
    {
        if (waitMs < minWaitMs || waitMs > maxWaitMs || i < 0)
        {
            waitMs = maxWaitMs;
            increase = false;
            backwards = false;
            i = 0;
        }
        if (LoaderChars == null || LoaderChars.Length < 2)
        {
            LoaderChars = defaultSymbolsList.GetRandomOrDefault().ToCharArray();
        }
        Array.Clear(loaderPositions, 0, loaderSlots);
        Console.ForegroundColor = LastForegroundColor;
        Console.BackgroundColor = LastBackgroundColor;

    }

    public static async Task Wait(CancellationToken cancellationToken)
    {
        CheckLoader();
        int loaderSlot = GetNextLoaderSlot();
        int countRandom = 0;
        int currCol = Console.CursorLeft;
        int acumulatedWaitMs = 0;
        bool canCheckJackpot = false;
        int lastLoaderSlot = -1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (acumulatedWaitMs >= waitMsChangeLoaderPosition)
            {
                acumulatedWaitMs -= waitMsChangeLoaderPosition;
                loaderSlot = GetNextLoaderSlot();
                if (lastLoaderSlot != loaderSlot)
                {
                    lastLoaderSlot = loaderSlot;
                    canCheckJackpot = true;
                }
                Console.SetCursorPosition(currCol + loaderSlot, Console.CursorTop);

            }
            acumulatedWaitMs += waitMs;
            if (countRandom > 0)
            {
                countRandom--;
                SpinLoader(Random.Shared.Next(0, LoaderChars.Length));
            }
            loaderPositions[loaderSlot] = GetLoaderChar();
            Console.Write(loaderPositions[loaderSlot]);
            if (canCheckJackpot)
            {
                canCheckJackpot = !CheckJackpot(loaderPositions, loaderSlots);
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
                ex.PrintAndWait();
            }
            finally
            {
                Console.Write("\b");
            }
        }
        Console.ResetColor();
    }


    private static bool CheckJackpot(char[] loaderPositions, int positions)
    {
        for (int j = 0; j < positions - 1; j++)
        {
            if (loaderPositions[j] == 0 || loaderPositions[j + 1] == 0)
            {
                return false;
            }
            if (loaderPositions[j] != loaderPositions[j + 1])
            {
                return false;
            }
        }
        Array.Clear(loaderPositions, 0, positions);
        int clippedColor = (int)ConsoleColor.Blue + ((int)Console.ForegroundColor + 1 - (int)ConsoleColor.Blue) % (((int)ConsoleColor.White - (int)ConsoleColor.Blue) + 1);
        Console.ForegroundColor = (ConsoleColor)clippedColor;
        if (Console.ForegroundColor == ConsoleColor.White)
        {
            // Increment Background Color
            int bgClippedColor = (int)ConsoleColor.Black + ((int)Console.BackgroundColor + 1 - (int)ConsoleColor.Black) % (((int)ConsoleColor.DarkYellow - (int)ConsoleColor.Black) + 1);
            Console.BackgroundColor = (ConsoleColor)bgClippedColor;
        }
        LastForegroundColor = Console.ForegroundColor;
        LastBackgroundColor = Console.BackgroundColor;
        return true;
    }

    private static int GetNextLoaderSlot()
    {
        for (int i = 0; i < loaderSlots; i++)
        {
            if (loaderPositions[i] == 0)
            {
                return i;
            }
        }
        return Random.Shared.Next(0, loaderSlots);
    }

    private static char GetLoaderChar()
    {
        char loaderChar = LoaderChars![loaderPosition];
        SpinLoader(1);
        return loaderChar;
    }

    private static void SpinLoader(int count)
    {
        if (count <= 0)
        {
            return;
        }
        count %= LoaderChars!.Length;
        if (backwards)
        {
            loaderPosition += (LoaderChars.Length - count);
        }
        else
        {
            loaderPosition += count;
        }
        loaderPosition %= LoaderChars.Length;
    }


    readonly private static int[] wait2Pattern = [300, 300, 200, 300, 100, 200, 100, 200];
    public static async Task Wait2(string symbols, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        bool randomizeSymbols = string.IsNullOrEmpty(symbols);
        try
        {
            if (!randomizeSymbols && LoaderChars.Length == 0)
            {
                LoaderChars = symbols.ToCharArray();
            }
            CheckLoader();

            int currCol = Console.CursorLeft;
            int iWaitPattern = 0;


            while (true)
            {
                try
                {
                    Raffle(loaderPositions, loaderSlots);

                    _ = CheckJackpot(loaderPositions, loaderSlots);

                    Console.Write(loaderPositions);
                    Console.CursorLeft = currCol;
                    await Task.Delay(wait2Pattern[iWaitPattern++ % wait2Pattern.Length], cancellationToken);
                }                
                catch 
                {
                    // Supress any
                    return;
                }
            }
        }
        finally
        {
            ResetLoader();
            if(randomizeSymbols)
            {
                Array.Clear(LoaderChars);
            }
        }
    }

    private static void ResetLoader()
    {
        Console.ResetColor();
        Array.Fill(loaderPositions, ' ');
        Console.WriteLine(loaderPositions);
        //Console.CursorLeft = currCol;
    }

    private static void Raffle(char[] loaderPositions, int positions)
    {
        for (int i = 0; i < positions; i++)
        {
            loaderPositions[i] = LoaderChars[Random.Shared.Next(0, LoaderChars.Length)];
        }
    }

    public static void Stop(CancellationTokenSource cts)
    {
        cts.Cancel();
        Thread.Sleep(50);
    }
}


