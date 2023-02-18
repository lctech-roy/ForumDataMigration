using Netcorext.Algorithms;
using Polly;

namespace ForumDataMigration.Extensions;

public static class SnowFlakeExtension
{
    public static long TryGenerate(this ISnowflake snowflake)
    {
        return Policy

                    // 1. 處理甚麼樣的例外
                   .Handle<ArgumentOutOfRangeException>()

                    // 2. 重試策略，包含重試次數
                   .Retry(5, (ex, retryCount) =>
                             {
                                 Console.WriteLine($"發生錯誤：{ex.Message}，第 {retryCount} 次重試");
                                 Thread.Sleep(3000);
                             })

                    // 3. 執行內容
                   .Execute(snowflake.Generate);
    }
}