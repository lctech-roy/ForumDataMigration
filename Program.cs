// See https://aka.ms/new-console-template for more information

using System.Globalization;
using ForumDataMigration;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Netcorext.Algorithms;

// 1. 建立依賴注入的容器
var serviceCollection = new ServiceCollection();

// 2. 註冊服務
serviceCollection.AddSingleton<ISnowflake>(_ => new SnowflakeJavaScriptSafeInteger((uint) new Random().Next(1, 31)));
serviceCollection.AddSingleton<Migration>();
serviceCollection.AddSingleton<ArticleRelationMigration>();

//serviceCollection.AddSingleton<ArticleCommentMigration>();
serviceCollection.AddSingleton<ArticleMigration>();
serviceCollection.AddSingleton<CommentMigration>();
serviceCollection.AddSingleton<ArticleRatingMigration>();
serviceCollection.AddSingleton<ArticleVoteMigration>();
serviceCollection.AddSingleton<ArticleRewardMigration>();
serviceCollection.AddSingleton<GameItemMigration>();
serviceCollection.AddSingleton<MemberBagMigration>();

// 建立依賴服務提供者
var serviceProvider = serviceCollection.BuildServiceProvider();

Directory.CreateDirectory(Setting.INSERT_DATA_PATH);
Directory.CreateDirectory(Setting.INSERT_DATA_PATH + "/Error");

// 3. 執行主服務
var migration = serviceProvider.GetRequiredService<Migration>();
var relationMigration = serviceProvider.GetRequiredService<ArticleRelationMigration>();

//var articleCommentMigration = serviceProvider.GetRequiredService<ArticleCommentMigration>();
var articleMigration = serviceProvider.GetRequiredService<ArticleMigration>();
var commentMigration = serviceProvider.GetRequiredService<CommentMigration>();
var ratingMigration = serviceProvider.GetRequiredService<ArticleRatingMigration>();
var voteMigration = serviceProvider.GetRequiredService<ArticleVoteMigration>();
var rewardMigration = serviceProvider.GetRequiredService<ArticleRewardMigration>();

var gameItemMigration = serviceProvider.GetRequiredService<GameItemMigration>();
var memberBagMigration = serviceProvider.GetRequiredService<MemberBagMigration>();

var token = new CancellationTokenSource().Token;

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

//var result = AttachmentHelper.GetArtifactAttachmentDic();

// //1.文章Id關聯表
// relationMigration.Migration();
// migration.ExecuteRelation();
//
// 3.文章,留言
await CommonHelper.WatchTimeAsync(nameof(articleMigration), async () => await articleMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteArticleAsync), async () => await migration.ExecuteArticleAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(CommentMigration), async () => await commentMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteCommentAsync), async () => await migration.ExecuteCommentAsync(token));

//
// //4.文章評分
// await CommonHelper.WatchTimeAsync("rating", async () => await ratingMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync("copy rating", async () => await migration.ExecuteRatingAsync(token));

//
// //5.文章投票
// CommonHelper.WatchTime("vote", () => voteMigration.Migration());
// await CommonHelper.WatchTimeAsync("copy vote", async () => await Migration.ExecuteArticleVoteAsync(token));

// //5.文章懸賞
// await CommonHelper.WatchTimeAsync("reward", async () => await rewardMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync("copy reward", async () => await migration.ExecuteArticleRewardAsync(token));

//6.遊戲中心
//CommonHelper.WatchTime(nameof(gameItemMigration),()=> gameItemMigration.Migration());
//await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteGameItemAsync), async () => await migration.ExecuteGameItemAsync());
// CommonHelper.WatchTime(nameof(memberBagMigration),()=> memberBagMigration.Migration());
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteMemberBagAsync), async () => await migration.ExecuteMemberBagAsync());

Console.WriteLine("Hello, World!");