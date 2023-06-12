// See https://aka.ms/new-console-template for more information

using System.Globalization;
using ForumDataMigration;
using ForumDataMigration.Helper;
using Lctech.Comment.Settings;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Netcorext.Algorithms;


// 1. 建立依賴注入的容器
var serviceCollection = new ServiceCollection();

// 2. 註冊服務
serviceCollection.AddSingleton<ISnowflake>(_ => new SnowflakeJavaScriptSafeInteger((uint)new Random().Next(1, 31)));
serviceCollection.AddSingleton<Migration>();
serviceCollection.AddSingleton<AttachmentMigration>();
serviceCollection.AddSingleton<ArticleMigration>();
serviceCollection.AddSingleton<CommentMigration>();
serviceCollection.AddSingleton<AttachmentCommentIdMigration>();
serviceCollection.AddSingleton<ArticleDeletionMigration>();
serviceCollection.AddSingleton<ArticleRatingMigration>();
serviceCollection.AddSingleton<ArticleVoteMigration>();
serviceCollection.AddSingleton<ArticleRewardMigration>();
serviceCollection.AddSingleton<GameItemMigration>();
serviceCollection.AddSingleton<MemberBagMigration>();
serviceCollection.AddSingleton<ParticipleMigration>();
serviceCollection.AddSingleton<ArticleBlackListMemberMigration>();
serviceCollection.AddSingleton<TaskMigration>();

serviceCollection.AddSingleton<FileExtensionContentTypeProvider>(_ =>
                                                                 {
                                                                     var fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();
                                                                     fileExtensionContentTypeProvider.Mappings.Add(".heif", "image/heif");
                                                                     fileExtensionContentTypeProvider.Mappings.Add(".heic", "image/heic");

                                                                     return fileExtensionContentTypeProvider;
                                                                 });

// 建立依賴服務提供者
var serviceProvider = serviceCollection.BuildServiceProvider();

Directory.CreateDirectory(Setting.INSERT_DATA_PATH);
Directory.CreateDirectory(Setting.INSERT_DATA_PATH + "/Error");

// 3. 執行主服務
var migration = serviceProvider.GetRequiredService<Migration>();

var attachmentMigration = serviceProvider.GetRequiredService<AttachmentMigration>();
var articleMigration = serviceProvider.GetRequiredService<ArticleMigration>();
var commentMigration = serviceProvider.GetRequiredService<CommentMigration>();
var attachmentCommentIdMigration = serviceProvider.GetRequiredService<AttachmentCommentIdMigration>();
var articleDeletionMigration = serviceProvider.GetRequiredService<ArticleDeletionMigration>();
var ratingMigration = serviceProvider.GetRequiredService<ArticleRatingMigration>();
var voteMigration = serviceProvider.GetRequiredService<ArticleVoteMigration>();
var rewardMigration = serviceProvider.GetRequiredService<ArticleRewardMigration>();

var gameItemMigration = serviceProvider.GetRequiredService<GameItemMigration>();
var memberBagMigration = serviceProvider.GetRequiredService<MemberBagMigration>();
var participleMigration = serviceProvider.GetRequiredService<ParticipleMigration>();
var blackListMigration = serviceProvider.GetRequiredService<ArticleBlackListMemberMigration>();
var taskMigration = serviceProvider.GetRequiredService<TaskMigration>();

var token = new CancellationTokenSource().Token;

Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

// 2.附件
// await CommonHelper.WatchTimeAsync(nameof(attachmentMigration), async () => await attachmentMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteAttachmentAsync), () => migration.ExecuteAttachmentAsync());

// 3.文章,留言
// await CommonHelper.WatchTimeAsync(nameof(articleMigration), async () => await articleMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteArticleAsync), async () => await migration.ExecuteArticleAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(CommentMigration), async () => await commentMigration.MigrationAsync(token)); 
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteCommentAsync), async () => await migration.ExecuteCommentAsync(token));

// 3.1. 留言Attachment CommentId extend data
// await CommonHelper.WatchTimeAsync(nameof(attachmentCommentIdMigration), async () => await attachmentCommentIdMigration.MigrationAsync(token));
// CommonHelper.WatchTime(nameof(migration.ExecuteAttachmentCommentId), () => migration.ExecuteAttachmentCommentId());

// 文章Deletion ##舊的暫時不用轉##
// await CommonHelper.WatchTimeAsync(nameof(articleDeletionMigration), async () => await articleDeletionMigration.MigrationAsync(token)); 
// CommonHelper.WatchTime(nameof(migration.ExecuteArticleDeletion), () => migration.ExecuteArticleDeletion());

//
// //4.文章評分
// await CommonHelper.WatchTimeAsync("rating", async () => await ratingMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync("copy rating", async () => await migration.ExecuteRatingAsync(token));

//
// //5.文章投票
// CommonHelper.WatchTime("vote", () => voteMigration.Migration());
// await CommonHelper.WatchTimeAsync("copy vote", async () => await Migration.ExecuteArticleVoteAsync(token));

// //5.文章懸賞
// await CommonHelper.WatchTimeAsync("reward", async () => await ArticleRewardMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync("copy reward", async () => await migration.ExecuteArticleRewardAsync(token));

//6.遊戲中心
// CommonHelper.WatchTime(nameof(gameItemMigration),()=> gameItemMigration.Migration());
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteGameItemAsync), async () => await migration.ExecuteGameItemAsync());
// CommonHelper.WatchTime(nameof(memberBagMigration),()=> memberBagMigration.Migration());
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteMemberBagAsync), async () => await migration.ExecuteMemberBagAsync());

//7.敏感字
// await CommonHelper.WatchTimeAsync(nameof(participleMigration),async () => await participleMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteParticipleAsync), async () => await migration.ExecuteParticipleAsync());

//8.1128黑名單
// await CommonHelper.WatchTimeAsync(nameof(blackListMigration),async () => await blackListMigration.MigrationAsync(token));
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteArticleBlackListMemberAsync), async () => await migration.ExecuteArticleBlackListMemberAsync());

//9.任務設定
// CommonHelper.WatchTime(nameof(TaskMigration), () => taskMigration.Migration());
// await CommonHelper.WatchTimeAsync(nameof(migration.ExecuteTaskAsync), async () => await migration.ExecuteTaskAsync());

Console.WriteLine("Hello, World!");