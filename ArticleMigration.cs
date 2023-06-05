using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Core.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using Lctech.Jkf.Forum.Enums;
using MySqlConnector;
using Netcorext.Algorithms;
using Polly;

namespace ForumDataMigration;

public class ArticleMigration
{
    private const string COPY_PREFIX = $"COPY \"{nameof(Article)}\" " +
                                       $"(\"{nameof(Article.Id)}\",\"{nameof(Article.BoardId)}\",\"{nameof(Article.CategoryId)}\",\"{nameof(Article.Status)}\",\"{nameof(Article.DeleteStatus)}\"" +
                                       $",\"{nameof(Article.VisibleType)}\",\"{nameof(Article.Type)}\",\"{nameof(Article.ContentType)}\",\"{nameof(Article.PinType)}\"" +
                                       $",\"{nameof(Article.Title)}\",\"{nameof(Article.Content)}\",\"{nameof(Article.ViewCount)}\",\"{nameof(Article.ReplyCount)}\"" +
                                       $",\"{nameof(Article.SortingIndex)}\",\"{nameof(Article.LastReplyDate)}\",\"{nameof(Article.LastReplierId)}\"" +
                                       $",\"{nameof(Article.Cover)}\",\"{nameof(Article.Tag)}\",\"{nameof(Article.RatingCount)}\",\"{nameof(Article.Warning)}\"" +
                                       $",\"{nameof(Article.ShareCount)}\",\"{nameof(Article.ImageCount)}\",\"{nameof(Article.VideoCount)}\",\"{nameof(Article.DonatePoint)}\"" +
                                       $",\"{nameof(Article.HighlightColor)}\",\"{nameof(Article.ReadPermission)}\",\"{nameof(Article.ContentSummary)}\"" +
                                       $",\"{nameof(Article.CommentVisibleType)}\",\"{nameof(Article.LikeCount)}\",\"{nameof(Article.UnlockHideCount)}\",\"{nameof(Article.Ip)}\"" +
                                       $",\"{nameof(Article.Price)}\",\"{nameof(Article.AuditorId)}\",\"{nameof(Article.AuditFloor)}\",\"{nameof(Article.PublishDate)}\",\"{nameof(Article.VisibleTime)}\",\"{nameof(Article.KeywordModificationDate)}\"" +
                                       $",\"{nameof(Article.HideBbCodeExpirationDate)}\",\"{nameof(Article.PinExpirationDate)}\",\"{nameof(Article.RecommendExpirationDate)}\",\"{nameof(Article.HighlightExpirationDate)}\"" +
                                       $",\"{nameof(Article.CommentDisabledExpirationDate)}\",\"{nameof(Article.InVisibleArticleExpirationDate)}\",\"{nameof(Article.Signature)}\",\"{nameof(Article.FreeType)}\",\"{nameof(Article.HotScore)}\"" +
                                       Setting.COPY_ENTITY_SUFFIX;

    private const string ARTICLE_ATTACHMENT_PREFIX = $"COPY \"{nameof(ArticleAttachment)}\" " +
                                                     $"(\"{nameof(ArticleAttachment.Id)}\",\"{nameof(ArticleAttachment.AttachmentId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private static readonly HashSet<long> BoardIdHash = RelationHelper.GetBoardIdHash();
    private static readonly HashSet<long> CategoryIdHash = RelationHelper.GetCategoryIdHash();
    private static readonly HashSet<long> ProhibitMemberIdHash = MemberHelper.GetProhibitMemberIdHash();
    private static readonly Dictionary<(int, string), int?> ModDic = ArticleHelper.GetModDic();
    private static readonly Dictionary<long, Read> ReadDic = ArticleHelper.GetReadDic();
    private static readonly Dictionary<string, long> MemberNameDic = MemberHelper.GetMemberNameDic();

    private static readonly Dictionary<int, string?> ColorDic = new()
                                                                {
                                                                    { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                                };

    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();

    private const string IMAGE_PATTERN = @"\[(img|attachimg)[^\]]*](.*?)\[\/\1]";
    private const string VIDEO_PATTERN = @"\[(media|video)=?([^\]]*)](.*?)\[\/\1]";
    private const string HIDE_PATTERN = @"\[(hide)[^\]]*]([\s\S]*)\[\/\1]";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string ARTICLE_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
    private const string ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}_{nameof(Article)}";
    private const string ARTICLE_ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleAttachment)}";

    private const string QUERY_ARTICLE_SQL = @"SELECT thread.tid,post.pid,post.invisible,thread.special
                                              ,postDelay.post_time AS postTime,thread.subject,post.message,thread.views
                                              ,thread.replies,thread.fid,thread.typeid,post.dateline
                                              ,thread.lastpost,thread.lastposter
                                              ,thread.cover,thread.thumb,post.tags,thread.sharetimes
                                              ,thread.digest,thread.readperm,thread.closed
                                              ,thread.status,thankCount.count AS thankCount,post.useip,post.usesig
                                              ,thread.price,thread.authorid,
                                              CASE WHEN top.issticky = 1 THEN 4
	                                               WHEN top.issticky = 2 THEN 5
	                                               WHEN thread.fid != 1128 THEN thread.displayorder
	                                               ELSE 0
                                              END AS displayorder,
                                              CASE WHEN top.color IS NOT NULL THEN top.color
	                                               WHEN thread.fid != 1128 THEN thread.highlight
                                                   ELSE 0
                                              END AS highlight,
                                              top.hexpiry,top.sexpiry
                                              FROM pre_forum_thread AS thread
                                              LEFT JOIN (SELECT * FROM pre_forum_post{0} WHERE `first` AND position = 1) post ON post.tid = thread.tid
                                              LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid
                                              LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = thread.tid
                                              LEFT JOIN pre_forum_topthreads top ON top.tid = thread.tid
                                              WHERE thread.posttableid = @postTableId AND thread.dateline >= @Start AND thread.dateline < @End AND post.tid is not null AND thread.displayorder >= -3";

    private static readonly ISnowflake AttachmentSnowflake = new SnowflakeJavaScriptSafeInteger(1);
    private const long MAX_UNIX_TIME = 32535215999;
    private static readonly DateTimeOffset MaxDate = DateTimeOffset.FromUnixTimeSeconds(MAX_UNIX_TIME);
    private static readonly DateTimeOffset MinDate = DateTimeOffset.FromUnixTimeSeconds(Constants.MINIMUM_EXPIRATION_UNIX_TIME_SECONDS);
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        RetryHelper.CreateArticleRetryTable();

        if (Setting.USE_UPDATED_DATE)
            RetryHelper.SetArticleRetry(RetryHelper.GetEarliestCreateDateStr(), null, string.Empty);

        var folderName = RetryHelper.GetArticleRetryDateStr();
        var postTableIds = ArticleHelper.GetPostTableIds();
        var periods = PeriodHelper.GetPeriods(folderName);

        if (folderName != null)
            Console.WriteLine("Retry on:" + folderName);

        if (Setting.TestTid != null)
            Console.WriteLine("Start Test:" + Setting.TestTid);

        Thread.Sleep(3000);

        //刪掉之前轉過的檔案
        if (folderName != null)
            FileHelper.RemoveFilesByDate(new[] { ARTICLE_PATH, ATTACHMENT_PATH, ARTICLE_ATTACHMENT_PATH }, folderName);
        else
            FileHelper.RemoveFiles(new[] { ARTICLE_PATH, ATTACHMENT_PATH, ARTICLE_ATTACHMENT_PATH });

        foreach (var period in periods)
        {
            try
            {
                await Parallel.ForEachAsync(postTableIds,
                                            CommonHelper.GetParallelOptions(cancellationToken),
                                            async (postTableId, token) =>
                                            {
                                                var sql = string.Concat(string.Format(QUERY_ARTICLE_SQL, postTableId == 0 ? "" : $"_{postTableId}"));

                                                await Policy

                                                      // 1. 處理甚麼樣的例外
                                                     .Handle<EndOfStreamException>()
                                                     .Or<ArgumentOutOfRangeException>()

                                                      // 2. 重試策略，包含重試次數
                                                     .RetryAsync(5, (ex, retryCount) =>
                                                                    {
                                                                        Console.WriteLine($"發生錯誤：{ex.Message}，第 {retryCount} 次重試");
                                                                        Thread.Sleep(3000);
                                                                    })

                                                      // 3. 執行內容
                                                     .ExecuteAsync(async () =>
                                                                   {
                                                                       if (Setting.TestTid != null)
                                                                           sql += $" AND thread.tid = {Setting.TestTid}";

                                                                       await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                       var command = new CommandDefinition(sql, new { postTableId, Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);

                                                                       var posts = (await cn.QueryAsync<ArticlePost>(command)).ToList();

                                                                       if (!posts.Any())
                                                                           return;

                                                                       await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                                                   });
                                            });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync($"{Setting.INSERT_DATA_PATH}/Error.txt", $"{period.FolderName}{Environment.NewLine}{e}", cancellationToken);
                RetryHelper.SetArticleRetry(period.FolderName, null, e.ToString());

                throw;
            }
        }
    }

    private static async Task ExecuteAsync(List<ArticlePost> posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var attachmentSb = new StringBuilder();
        var articleAttachmentSb = new StringBuilder();

        var articleIdHash = new HashSet<int>();

        foreach (var post in posts)
        {
            if (!BoardIdHash.Contains(post.Fid))
                continue;

            // 排除因為lastPoster重複的文章
            if (articleIdHash.Contains(post.Tid))
                continue;

            articleIdHash.Add(post.Tid);

            post.CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline);
            post.CreateMilliseconds = post.Dateline * 1000L;

            try
            {
                SetArticle(post, sb, attachmentSb, articleAttachmentSb,period,postTableId);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Tid:{post.Tid}");
                Console.WriteLine(e);

                throw;
            }
        }

        var task = new Task(() => { FileHelper.WriteToFile($"{ARTICLE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_PREFIX, sb); });

        var attachmentTask = new Task(() => { FileHelper.WriteToFile($"{ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", AttachmentHelper.ATTACHMENT_PREFIX, attachmentSb); });

        var articleAttachmentTask = new Task(() => { FileHelper.WriteToFile($"{ARTICLE_ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", ARTICLE_ATTACHMENT_PREFIX, articleAttachmentSb); });

        task.Start();
        attachmentTask.Start();
        articleAttachmentTask.Start();

        await Task.WhenAll(task, attachmentTask, articleAttachmentTask);
    }

    private static void SetArticle(ArticlePost post, StringBuilder sb, StringBuilder attachmentSb, StringBuilder articleAttachmentSb,Period period,int postTableId)
    {
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = ReadDic.GetValueOrDefault(post.Tid);
        var publishDate = post.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.PostTime.Value) : post.CreateDate;

        var pinType = post.Displayorder switch
                      {
                          1 => PinType.Board,
                          2 => PinType.Area,
                          3 => PinType.Global,
                          4 => PinType.Advertise,
                          5 => PinType.Advertise5X,
                          _ => PinType.None
                      };

        var article = new Article
                      {
                          Id = post.Tid,
                          Status = ArticleStatus.Published,
                          DeleteStatus = post.Invisible | ProhibitMemberIdHash.Contains(post.Authorid) ? DeleteStatus.Deleted : DeleteStatus.None,
                          Type = post.Special switch
                                 {
                                     1 => ArticleType.Vote,
                                     2 => ArticleType.Diversion,
                                     3 => ArticleType.Reward,
                                     _ => post.Fid is 228 or 209 ? ArticleType.Serialized : ArticleType.Article
                                 },
                          PinType = pinType,
                          VisibleType = post.Status == 1 ? VisibleType.Hidden : VisibleType.Public,
                          Title = RegexHelper.GetNewSubject(post.Subject),
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid % 10, post.Pid, post.Tid,
                                                              post.Authorid, post.CreateDate, attachmentSb, articleAttachmentSb),
                          ViewCount = post.Views,
                          ReplyCount = post.Replies,
                          HotScore = post.Views / 100 + post.Replies,
                          BoardId = post.Fid,
                          CategoryId = CategoryIdHash.Contains(post.Typeid) ? post.Typeid : null,
                          SortingIndex = post.CreateMilliseconds,
                          LastReplyDate = post.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,
                          LastReplierId = MemberNameDic.TryGetValue(post.Lastposter, out var value) ? value : null,
                          Cover = SetCoverAttachment(post, attachmentSb),
                          Tag = post.Tags.ToNewTags(),
                          RatingCount = post.Ratetimes ?? 0,
                          ShareCount = post.Sharetimes,
                          DonatePoint = 0,
                          HighlightColor = ColorDic.GetValueOrDefault(highlightInt),
                          ReadPermission = post.Readperm,
                          CommentVisibleType = post.Status == 34 ? VisibleType.Private : VisibleType.Public,
                          LikeCount = post.ThankCount ?? 0,
                          UnlockHideCount = post.ThankCount ?? 0,
                          Ip = post.Useip,
                          Price = post.Price,
                          AuditorId = read?.ReadUid,
                          AuditFloor = read?.ReadFloor,
                          PublishDate = publishDate,
                          HideBbCodeExpirationDate = BbCodeHideTagRegex.IsMatch(post.Message) ? publishDate.AddDays(CommonSetting.HideExpirationDay) : null,
                          PinExpirationDate = post.Sexpiry.HasValue
                                                  ? DateTimeOffset.FromUnixTimeSeconds(post.Sexpiry.Value)
                                                  : ModDic.GetValueOrDefault((post.Tid, "EST")).ToDatetimeOffset()
                                                 ?? (pinType != PinType.None ? MaxDate : MinDate),
                          HighlightExpirationDate = post.Hexpiry.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Hexpiry.Value) : ModDic.GetValueOrDefault((post.Tid, "EHL")).ToDatetimeOffset() ?? (post.Highlight != 0 ? MaxDate : null),
                          RecommendExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EDI")).ToDatetimeOffset() ?? (post.Digest ? MaxDate : null),
                          CommentDisabledExpirationDate = ModDic.GetValueOrDefault((post.Tid, "ECL")).ToDatetimeOffset() ?? (post.Closed == 1 ? MaxDate : null),
                          InVisibleArticleExpirationDate = ModDic.GetValueOrDefault((post.Tid, "BNP")).ToDatetimeOffset() ??
                                                           ModDic.GetValueOrDefault((post.Tid, "UBN")).ToDatetimeOffset(),
                          KeywordModificationDate = post.CreateDate,
                          Signature = post.Usesig,
                          CreatorId = post.Authorid,
                          ModifierId = post.Authorid,
                          CreationDate = post.CreateDate,
                          ModificationDate = post.CreateDate
                      };

        article.ImageCount = BbCodeImageRegex.Matches(article.Content).Count;
        article.VideoCount = BbCodeVideoRegex.Matches(article.Content).Count;

        article.ContentType = article is { ImageCount: > 0, VideoCount: > 0 }
                                  ? ContentType.ImageAndVideo
                                  : article.ImageCount > 0
                                      ? ContentType.Image
                                      : article.VideoCount > 0
                                          ? ContentType.Video
                                          : ContentType.PaintText;

        article.ContentSummary = (article.Price > 0
                                      ? article.Content.GetFreeContent().RemoveHideContent()
                                      : article.Content.RemoveHideContent()
                                 ).GetContentSummary();

        article.VisibleTime = article.DeleteStatus == DeleteStatus.Deleted || article.VisibleType == VisibleType.Hidden ? MAX_UNIX_TIME : article.PublishDate.ToUnixTimeSeconds();

        AppendArticleSb(article, sb, period, postTableId);
        
        // sb.AppendValueLine(article.Id, article.BoardId, article.CategoryId.ToCopyValue(), (int) article.Status, (int) article.DeleteStatus,
        //                    (int) article.VisibleType, (int) article.Type, (int) article.ContentType, (int) article.PinType, article.Title.ToCopyText(),
        //                    article.Content.ToCopyText(), article.ViewCount, article.ReplyCount, article.SortingIndex, article.LastReplyDate.ToCopyValue(),
        //                    article.LastReplierId.ToCopyValue(), article.Cover.ToCopyValue(), article.Tag.ToCopyText(), article.RatingCount, article.Warning,
        //                    article.ShareCount, article.ImageCount, article.VideoCount, article.DonatePoint, article.HighlightColor.ToCopyValue(),
        //                    article.ReadPermission, article.ContentSummary.ToCopyText(), (int) article.CommentVisibleType, article.LikeCount, article.UnlockHideCount,
        //                    article.Ip, article.Price, article.AuditorId.ToCopyValue(), article.AuditFloor.ToCopyValue(), article.PublishDate, 
        //                    article.VisibleTime, article.KeywordModificationDate, article.HideBbCodeExpirationDate.ToCopyValue(), article.PinExpirationDate.ToCopyValue(),
        //                    article.RecommendExpirationDate.ToCopyValue(), article.HighlightExpirationDate.ToCopyValue(), article.CommentDisabledExpirationDate.ToCopyValue(),
        //                    article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature, (int) article.FreeType, article.HotScore,
        //                    article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);
    }

    private static void AppendArticleSb(Article article, StringBuilder articleSb, Period period, int postTableId)
    {
        const int maxStringBuilderLength = 600000;

        if (articleSb.Length > maxStringBuilderLength)
        {
            FileHelper.WriteToFile($"{ARTICLE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_PREFIX, articleSb); 
        }

        articleSb.AppendValueLine(article.Id, article.BoardId, article.CategoryId.ToCopyValue(), (int) article.Status, (int) article.DeleteStatus,
                                  (int) article.VisibleType, (int) article.Type, (int) article.ContentType, (int) article.PinType, article.Title.ToCopyText(),
                                  article.Content.ToCopyText(), article.ViewCount, article.ReplyCount, article.SortingIndex, article.LastReplyDate.ToCopyValue(),
                                  article.LastReplierId.ToCopyValue(), article.Cover.ToCopyValue(), article.Tag.ToCopyText(), article.RatingCount, article.Warning,
                                  article.ShareCount, article.ImageCount, article.VideoCount, article.DonatePoint, article.HighlightColor.ToCopyValue(),
                                  article.ReadPermission, article.ContentSummary.ToCopyText(), (int) article.CommentVisibleType, article.LikeCount, article.UnlockHideCount,
                                  article.Ip, article.Price.ToCopyValue(), article.AuditorId.ToCopyValue(), article.AuditFloor.ToCopyValue(), article.PublishDate, 
                                  article.VisibleTime, article.KeywordModificationDate, article.HideBbCodeExpirationDate.ToCopyValue(), article.PinExpirationDate.ToCopyValue(),
                                  article.RecommendExpirationDate.ToCopyValue(), article.HighlightExpirationDate.ToCopyValue(), article.CommentDisabledExpirationDate.ToCopyValue(),
                                  article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature, (int) article.FreeType, article.HotScore,
                                  article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);
    }
    
    private static long? SetCoverAttachment(ArticlePost post, StringBuilder attachmentSb)
    {
        var isCover = post.Cover is not ("" or "0");
        var externalLink = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb);

        if (string.IsNullOrEmpty(externalLink))
            return null;

        var attachment = new Attachment
                         {
                             Id = AttachmentSnowflake.TryGenerate(),
                             ExternalLink = externalLink,
                             CreationDate = post.CreateDate,
                             CreatorId = post.Authorid,
                             ModificationDate = post.CreateDate,
                             ModifierId = post.Authorid
                         };

        attachmentSb.AppendAttachmentValue(attachment);

        return attachment.Id;
    }
}