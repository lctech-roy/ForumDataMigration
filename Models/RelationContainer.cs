using System.Collections.Concurrent;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Microsoft.VisualStudio.Threading;
using Npgsql;

namespace ForumDataMigration;

public static class RelationContainer
{
    public static Dictionary<int, long> ArticleIdDic = new();

    // public ConcurrentDictionary<int, long> ArticleIdDic => DicInitializer.GetValue();
    //
    // private AsyncLazy<ConcurrentDictionary<int, long>> DicInitializer { get; }
    //
    // public IdContainer()
    //     => DicInitializer = new AsyncLazy<ConcurrentDictionary<int, long>>(InitializeDicAsync, new JoinableTaskFactory(new JoinableTaskContext()));
    //
    // private static async Task<ConcurrentDictionary<int, long>> InitializeDicAsync()
    // {
    //     const string path = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRelation)}";
    //     var periods = PeriodHelper.GetPeriods();
    //
    //     var articleDic = new ConcurrentDictionary<int, long>();
    //     
    //     await Parallel.ForEachAsync(periods,new ParallelOptions(){MaxDegreeOfParallelism = 20},
    //                                 async (period,_) =>
    //                                 {
    //                                     if (!File.Exists($"{path}/{period.FileName}")) return;
    //
    //                                     using var fs = File.OpenText($"{path}/{period.FileName}");
    //                                     
    //                                     //跳過第一行
    //                                     await fs.ReadLineAsync();
    //                                     
    //                                     var lineData = await fs.ReadLineAsync();
    //                          
    //                                     while (!string.IsNullOrWhiteSpace(lineData))
    //                                     {
    //                                         var row = lineData.Split(Setting.D);
    //                                         var isAdd = articleDic.TryAdd(int.Parse(row[1]), long.Parse(row[0]));
    //                                         
    //                                         if(!isAdd)
    //                                             Console.WriteLine(row + " Add To ArticleDic Failed!");
    //                                         
    //                                         lineData = await fs.ReadLineAsync();
    //                                     }
    //                          
    //                                     Console.WriteLine(period.FileName);
    //                                 });
    //     
    //     return articleDic;
}