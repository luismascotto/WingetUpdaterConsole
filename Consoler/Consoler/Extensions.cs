using System.Text;

namespace Consoler;

public static class Extensions
{
    public static bool IsEmpty(this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }

    public static bool HasSomething(this string? str)
    {
        return !IsEmpty(str);
    }

    public static void Shrink<T>(this List<T> list)
    {
        if (list != default)
        {
            list.Clear();
            list.TrimExcess();
        }
    }

    public static bool AnySafe<T>(this List<T> list)
    {
        if (list != default)
        {
            return list.Count > 0;
        }
        return false;
    }

    public static int CountSafe<T>(this List<T> list)
    {
        if (list != default)
        {
            return list.Count;
        }
        return 0;
    }

    public static bool AnySafe<T>(this IEnumerable<T> list)
    {
        if (list != default)
        {
            return list.Any();
        }
        return false;
    }

    public static string MyFileTimestamp(this DateTime dt)
    {
        //{DateTime.Now:yyyy-MM-dd}_{DateTime.Now.Ticks:X16}
        return $"{dt:yyyy-MM-dd}_{dt.Ticks:X16}"; //2023-11-03_000000018E1F6C80
    }

    public static void Print(this Exception ex, string message = "")
    {
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        if (message.HasSomething())
        {
            Console.WriteLine(message);
        }
        Console.WriteLine(ex.Message);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Functions.WriteLineWrapIndented(ex.StackTrace);
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintAndWait(this Exception ex, string message = "")
    {
        ex.Print(message);
        Functions.WaitEnterKeyUpTo(30000).Wait();
    }

    public static void AppendToTimestampFile(this Exception ex, string path, string nameSuffix)
    {
        try
        {
            File.AppendAllText(
                Path.Join(path, $"{nameSuffix}_{DateTime.Now.MyFileTimestamp()}.txt"),
                ex.ToString()
            );
        }
        catch (Exception aex)
        {
            Console.WriteLine($"Erro ao salvar exceção: {aex.Message}");
        }
    }

    public static void AppendToTimestampFile(this StringBuilder sb, string path, string nameSuffix)
    {
        try
        {
            using var writer = new StreamWriter(
                Path.Join(path, $"{nameSuffix}_{DateTime.Now.MyFileTimestamp()}.txt"),
                append: true,
                Encoding.UTF8
            );

            // Iterate through internal chunks without materializing the whole string
            foreach (ReadOnlyMemory<char> chunk in sb.GetChunks())
            {
                writer.Write(chunk.Span);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar output: {ex.Message}");
        }
    }

    public static T GetRandomOrDefault<T>(this List<T>? list)
    {
        if (list != default && list.Count > 0)
        {
            return list[Random.Shared.Next(0, list.Count)];
        }
        return default!;
    }

    public static T GetRandomOrDefault<T>(this T[]? array)
    {
        if (array != default && array.Length > 0)
        {
            return array[Random.Shared.Next(0, array.Length)];
        }
        return default!;
    }

    public static int ParseNatural(this ReadOnlySpan<char> str, int defaultValue = -1)
    {
        if (int.TryParse(str, out int result) && result >= 0)
        {
            return result;
        }
        if (str.Length == 1)
        {
            return str[0];
        }
        return defaultValue;
    }
}
