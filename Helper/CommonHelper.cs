using System.Diagnostics;

namespace ForumDataMigration.Helper;

public class CommonHelper
{
    public static void WatchTime(string actionName,Action action)
    {
         var sw = new Stopwatch();
         sw.Start();
         action();
         sw.Stop();
         Console.WriteLine($"{actionName} Time => {sw.ElapsedMilliseconds}ms");
         var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
         var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
         Console.WriteLine($"{actionName} Time => {answer}");
    }
    
    public static async Task WatchTimeAsync(string actionName,Func<Task> action)
    {
        var sw = new Stopwatch();
        sw.Start();
        await action();
        sw.Stop();
        Console.WriteLine($"{actionName} Time => {sw.ElapsedMilliseconds}ms");
        var t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
        var answer = $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s:{t.Milliseconds:D3}ms";
        Console.WriteLine($"{actionName} Time => {answer}");
    }
}