using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Consoler;

sealed class KeyWaiter : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationTokenSource _disposeCts = new();
    public CancellationToken Token => _cts.Token;

    //private readonly Stopwatch _stopwatch = new();

    private long _timeoutMs = 1000;
    private ConsoleKey _keyToWaitFor = ConsoleKey.None; //Any
    public KeyWaiter()
    {
        WaitForKeyPress();
    }
    public KeyWaiter(int timeoutMS) : base()
    {
        SetTimeout(timeoutMS);
        WaitForKeyPress();
    }
    public KeyWaiter(ConsoleKey keyToWaitFor) : base()
    {
        SetKey(keyToWaitFor);
        WaitForKeyPress();
    }

    public KeyWaiter(ConsoleKey keyToWaitFor, long timeoutMS) : base()
    {
        SetTimeout(timeoutMS);
        SetKey(keyToWaitFor);
        WaitForKeyPress();
    }

    private void SetTimeout(long timeoutMS)
    {
        if (timeoutMS > _timeoutMs)
        {
            _timeoutMs = timeoutMS;
        }
    }
    private void SetKey(ConsoleKey keyToWaitFor)
    {
        if (keyToWaitFor != ConsoleKey.None)
        {
            _keyToWaitFor = keyToWaitFor;
        }
    }


    private void WaitForKeyPress()
    {
        var task = Task.Run(async () =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                while (true)
                {
                    if (sw.ElapsedMilliseconds >= _timeoutMs ||
                    (Console.KeyAvailable && ((Console.ReadKey(true).Key == _keyToWaitFor || _keyToWaitFor == ConsoleKey.None))))
                    {
                        sw.Stop();
                        _cts.Cancel();
                        break;
                    }

                    await Task.Delay(50, _disposeCts.Token).ConfigureAwait(false);
                }
            }
            catch { }
        });
        task.Wait();
    }
    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _cts.Dispose();
    }
}
