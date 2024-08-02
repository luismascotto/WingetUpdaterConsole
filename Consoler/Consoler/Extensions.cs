using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
