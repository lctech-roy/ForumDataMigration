// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Globalization;
using ForumDataMigration;
using ForumDataMigration.Helper;
using Microsoft.Extensions.DependencyInjection;
using Netcorext.Algorithms;

// 1. 建立依賴注入的容器
var serviceCollection = new ServiceCollection();

// 2. 註冊服務
serviceCollection.AddSingleton<ISnowflake>(_ => new SnowflakeJavaScriptSafeInteger((uint)new Random().Next(1, 31)));
serviceCollection.AddSingleton<Migration>();
serviceCollection.AddSingleton<RelationMigration>();
serviceCollection.AddSingleton<ArticleCommentMigration>();
serviceCollection.AddSingleton<ArticleRatingMigration>();
serviceCollection.AddSingleton<ArticleVoteMigration>();

// 建立依賴服務提供者
var serviceProvider = serviceCollection.BuildServiceProvider();

// 3. 執行主服務
var migration = serviceProvider.GetRequiredService<Migration>();
var relation = serviceProvider.GetRequiredService<RelationMigration>();
var articleCommentMigration = serviceProvider.GetRequiredService<ArticleCommentMigration>();
var articleRatingMigration = serviceProvider.GetRequiredService<ArticleRatingMigration>();
var articleVoteMigration = serviceProvider.GetRequiredService<ArticleVoteMigration>();

var token = new CancellationTokenSource().Token;

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

// //1.文章Id關聯表
// relation.Migration();
// migration.ExecuteRelation();

// //2.寫入ArticleId Mapping表
//RelationContainer.ArticleIdDic =  RelationHelper.GetArticleDic();

// //3.文章,留言
// articleCommentMigration.Migration();
 await migration.ExecuteArticleAsync(token);
//await migration.ExecuteArticleRewardAsync(token);
// await migration.ExecuteCommentAsync(token);

// //4.文章評分
// articleRatingMigration.Migration();
// await migration.ExecuteRatingAsync(token);

// //5.文章投票
// articleVoteMigration.Migration();
// await migration.ExecuteArticleVoteAsync(token);

Console.WriteLine("Hello, World!");