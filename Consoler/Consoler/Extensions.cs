namespace Consoler;
public static class Extensions
{
    public static bool IsEmpty(this string str)
    {
        return string.IsNullOrWhiteSpace(str);
    }
    public static bool HasSomething(this string str)
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

    public static string MyFileTimestamp(this DateTime dt)
    {
        //{DateTime.Now:yyyy-MM-dd}_{DateTime.Now.Ticks:X16}
        return $"{dt:yyyy-MM-dd}_{dt.Ticks:X16}"; //2023-11-03_000000018E1F6C80
    }
}
