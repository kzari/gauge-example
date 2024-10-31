using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AgileContent.Itaas.E2E;

public static class RetryWrapper
{
    public static async Task<bool> Retry(int timeout, Func<Task<bool>> func)
    {
        bool condition;
        const int sleep = 5000;
        var sw = new Stopwatch();
        sw.Start();
        do
        {
            await Task.Delay(sleep);
            condition = await func.Invoke();
        } while (!condition && sw.Elapsed < TimeSpan.FromMilliseconds(timeout));
        sw.Stop();
        return condition;
    }
}