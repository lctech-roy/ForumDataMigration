using System.Diagnostics;
using Netcorext.Algorithms;

namespace ForumDataMigration.Helper;

public class CommonHelper
{
    public static void WatchTime(string actionName, Action action)
    {
        var sw = new Stopwatch();
        Console.WriteLine($"Start {actionName}");

        sw.Start();
        action();
        sw.Stop();

        var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
        var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
        Console.WriteLine($"{actionName} Time => {answer}");
    }

    public static T WatchTime<T>(string actionName, Func<T> action)
    {
        var sw = new Stopwatch();
        Console.WriteLine($"Start {actionName}");

        sw.Start();
        var result = action();
        sw.Stop();

        var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
        var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
        Console.WriteLine($"{actionName} Time => {answer}");

        return result;
    }

    public static async Task WatchTimeAsync(string actionName, Func<Task> action)
    {
        var sw = new Stopwatch();
        Console.WriteLine($"Start {actionName}");

        sw.Start();
        await action();
        sw.Stop();

        var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
        var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
        Console.WriteLine($"{actionName} Time => {answer}");
    }

    public static async Task<T> WatchTimeAsync<T>(string actionName, Func<Task<T>> action)
    {
        var sw = new Stopwatch();
        Console.WriteLine($"Start {actionName}");

        sw.Start();
        var result = await action();
        sw.Stop();

        var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
        var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
        Console.WriteLine($"{actionName} Time => {answer}");

        return result;
    }

    public static ParallelOptions GetParallelOptions(CancellationToken cancellationToken = default)
    {
        // return new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken };

        return new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.75 * 2.0)), CancellationToken = cancellationToken };
    }
}