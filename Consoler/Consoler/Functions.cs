using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Consoler;

public class Functions
{
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
            Console.WriteLine($"##########      EXCEPTION ({actual} vs ({found}))     ########");
            Console.WriteLine(ex.Message);
            Console.ResetColor();
            return 1;
        }
    }

   
}


