// See https://aka.ms/new-console-template for more information
using System.Globalization;
using ForumDataMigration;
using ForumDataMigration.Helper;
using Microsoft.Extensions.DependencyInjection;
using Netcorext.Algorithms;

// 1. 建立依賴注入的容器
var serviceCollection = new ServiceCollection();

// 2. 註冊服務
serviceCollection.AddSingleton<ISnowflake>(_ => new SnowflakeJavaScriptSafeInteger((uint) new Random().Next(1, 31)));
serviceCollection.AddSingleton<Migration>();
serviceCollection.AddSingleton<ArticleRelationMigration>();
serviceCollection.AddSingleton<ArticleCommentMigration>();
serviceCollection.AddSingleton<ArticleRatingMigration>();
serviceCollection.AddSingleton<ArticleVoteMigration>();

// 建立依賴服務提供者
var serviceProvider = serviceCollection.BuildServiceProvider();

// 3. 執行主服務
var migration = serviceProvider.GetRequiredService<Migration>();
var relationMigration = serviceProvider.GetRequiredService<ArticleRelationMigration>();
var commentMigration = serviceProvider.GetRequiredService<ArticleCommentMigration>();
var ratingMigration = serviceProvider.GetRequiredService<ArticleRatingMigration>();
var voteMigration = serviceProvider.GetRequiredService<ArticleVoteMigration>();

var token = new CancellationTokenSource().Token;

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

// //1.文章Id關聯表
// relationMigration.Migration();
// migration.ExecuteRelation();
//
// //2.寫入ArticleId Mapping表
// RelationContainer.ArticleIdDic = RelationHelper.GetArticleDic();
//
// //3.文章,留言
// commentMigration.Migration();
// await migration.ExecuteArticleAsync(token);
// await migration.ExecuteArticleRewardAsync(token);
// await migration.ExecuteCommentAsync(token);
//
// //4.文章評分
// await CommonHelper.WatchTimeAsync("rating", async () => await ratingMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync("copy rating", async () => await migration.ExecuteRatingAsync(token));
//
// //5.文章投票
// voteMigration.Migration();
// await migration.ExecuteArticleVoteAsync(token);

Console.WriteLine("Hello, World!");