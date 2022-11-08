using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Core.Domain;
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
                                       $",\"{nameof(Article.SortingIndex)}\",\"{nameof(Article.LastReplyDate)}\",\"{nameof(Article.LastReplierId)}\",\"{nameof(Article.PinPriority)}\"" +
                                       $",\"{nameof(Article.Cover)}\",\"{nameof(Article.Tag)}\",\"{nameof(Article.RatingCount)}\"" +
                                       $",\"{nameof(Article.ShareCount)}\",\"{nameof(Article.ImageCount)}\",\"{nameof(Article.VideoCount)}\",\"{nameof(Article.DonatePoint)}\"" +
                                       $",\"{nameof(Article.Highlight)}\",\"{nameof(Article.HighlightColor)}\",\"{nameof(Article.Recommend)}\",\"{nameof(Article.ReadPermission)}\"" +
                                       $",\"{nameof(Article.CommentDisabled)}\",\"{nameof(Article.CommentVisibleType)}\",\"{nameof(Article.LikeCount)}\",\"{nameof(Article.Ip)}\"" +
                                       $",\"{nameof(Article.Price)}\",\"{nameof(Article.AuditorId)}\",\"{nameof(Article.AuditFloor)}\",\"{nameof(Article.PublishDate)}\"" +
                                       $",\"{nameof(Article.HideExpirationDate)}\",\"{nameof(Article.PinExpirationDate)}\",\"{nameof(Article.RecommendExpirationDate)}\",\"{nameof(Article.HighlightExpirationDate)}\"" +
                                       $",\"{nameof(Article.CommentDisabledExpirationDate)}\",\"{nameof(Article.InVisibleArticleExpirationDate)}\",\"{nameof(Article.Signature)}\"" +
                                       Setting.COPY_ENTITY_SUFFIX;

    private const string ARTICLE_ATTACHMENT_PREFIX = $"COPY \"{nameof(ArticleAttachment)}\" " +
                                                     $"(\"{nameof(ArticleAttachment.Id)}\",\"{nameof(ArticleAttachment.AttachmentId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<int, long?> CategoryDic = RelationHelper.GetCategoryDic();
    private static readonly Dictionary<(int, string), int?> ModDic = ArticleHelper.GetModDic();
    private static readonly Dictionary<long, Read> ReadDic = ArticleHelper.GetReadDic();

    private static readonly Dictionary<int, string?> ColorDic = new()
                                                                {
                                                                    { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                                };

    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();
    private static readonly Dictionary<int, long> ArticleDic = RelationHelper.GetArticleDic();
    private static readonly Dictionary<long, (long, string)> MemberDic = RelationHelper.GetSimpleMemberDic();

    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";
    private const string HIDE_PATTERN = @"(\[\/?hide[^\]]*\]|{[^}]*})";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string ARTICLE_JSON = "ArticleJson";

    private const string ARTICLE_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
    private const string ARTICLE_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_JSON}";
    private const string ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}";
    private const string ARTICLE_ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleAttachment)}";
    private const string ARTICLE_COMBINE_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}.json";

    private static readonly string ArticleEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string ArticleEsIdSuffix = $"\" }}}}";
    private static readonly string ArticleRelationShipName = DocumentType.Thread.ToString().ToLower();

    private const string QUERY_ARTICLE_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                               SELECT thread.tid,thread.displayorder,thread.special
                                              ,postDelay.post_time AS postTime,thread.subject,post.message,thread.views
                                              ,thread.replies,thread.fid,thread.typeid,post.dateline
                                              ,thread.lastpost,postReply.authorid AS lastposter
                                              ,thread.cover,thread.thumb,post.tags,post.ratetimes,thread.sharetimes
                                              ,thread.highlight,thread.digest,thread.readperm,thread.closed
                                              ,thread.status,thankCount.count AS thankCount,post.useip,post.usesig
                                              ,thread.price,thread.authorid
                                              FROM pre_forum_thread AS thread
                                              LEFT JOIN pre_forum_post{0} post ON post.first = TRUE AND post.position = 1 AND post.tid = thread.tid
                                              LEFT JOIN pre_forum_post{0} postReply ON thread.replies > 0 AND postReply.tid = thread.tid AND postReply.dateline = thread.lastpost AND postReply.author = thread.lastposter
                                              LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid
                                              LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = thread.tid
                                              WHERE thread.posttableid = @postTableId AND thread.dateline >= @Start AND thread.dateline < @End AND post.tid is not null";

    private static readonly ISnowflake AttachmentSnowflake = new SnowflakeJavaScriptSafeInteger(1);

    //WHERE thread.posttableid = @postTableId AND thread.tid = 8128927";
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        RetryHelper.CreateArticleRetryTable();

        if (Setting.USE_UPDATED_DATE)
            RetryHelper.SetArticleRetry(RetryHelper.GetEarliestCreateDateStr(), null, string.Empty);

        var folderName = RetryHelper.GetArticleRetryDateStr();
        var postTableIds = ArticleHelper.GetPostTableIds();
        var periods = PeriodHelper.GetPeriods(folderName);

        //刪掉之前轉過的檔案
        if (folderName != null)
            RetryHelper.RemoveFilesByDate(new[] { ARTICLE_PATH, ARTICLE_JSON_PATH, ATTACHMENT_PATH, ARTICLE_ATTACHMENT_PATH }, folderName);
        else
            RetryHelper.RemoveFiles(new[] { ARTICLE_PATH, ARTICLE_JSON_PATH, ATTACHMENT_PATH, ARTICLE_ATTACHMENT_PATH, ARTICLE_COMBINE_JSON_PATH });

        foreach (var period in periods)
        {
            try
            {
                await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                              {
                                                                                                                  var posts = Array.Empty<ArticlePost>();

                                                                                                                  var sql = string.Concat(string.Format(QUERY_ARTICLE_SQL, postTableId == 0 ? "" : $"_{postTableId}"));


                                                                                                                  await using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                                                                                                  {
                                                                                                                      var command = new CommandDefinition(sql, new { postTableId, Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);

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
                                                                                                                           .ExecuteAsync(async () => { posts = (await cn.QueryAsync<ArticlePost>(command)).ToArray(); });
                                                                                                                  }

                                                                                                                  if (!posts.Any())
                                                                                                                      return;

                                                                                                                  await ExecuteAsync(posts.ToList(), postTableId, period, cancellationToken);
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

        // if (Setting.USE_UPDATED_DATE)
        await FileHelper.CombineMultipleFilesIntoSingleFileAsync(ARTICLE_JSON_PATH,
                                                                 "*.json",
                                                                 ARTICLE_COMBINE_JSON_PATH,
                                                                 cancellationToken);
    }

    private static async Task ExecuteAsync(List<ArticlePost> posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var jsonSb = new StringBuilder();
        var attachmentSb = new StringBuilder();
        var articleAttachmentSb = new StringBuilder();

        // var sw = new Stopwatch();
        // sw.Start();

        // sw.Stop();
        // Console.WriteLine($"selectMany Time => {sw.ElapsedMilliseconds}ms");

        // 排除因為lastPoster重複的文章
        posts = posts.DistinctBy(x => x.Tid).ToList();

        posts.RemoveAll(x => !ArticleDic.ContainsKey(x.Tid) ||     //髒資料放過他
                             !BoardDic.ContainsKey(x.Fid) ||       //髒資料放過他
                             !MemberDic.ContainsKey(x.Authorid) || //髒資料放過他
                             x.Displayorder == -4);                //文章草稿不處理

        var attachmentDic = await AttachmentHelper.GetAttachmentDicAsync(RegexHelper.GetAttachmentGroups(posts), AttachmentSnowflake, cancellationToken);

        foreach (var post in posts)
        {
            var id = ArticleDic[post.Tid];
            var boardId = BoardDic[post.Fid];
            var (memberId, memberName) = MemberDic[post.Authorid];

            var postResult = new ArticlePostResult()
                             {
                                 ArticleId = id,
                                 BoardId = boardId,
                                 MemberId = memberId,
                                 MemberName = memberName,
                                 CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline),
                                 CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000,
                                 Post = post,
                                 AttachmentDic = attachmentDic
                             };

            try
            {
                await SetArticleAsync(postResult, sb, jsonSb, attachmentSb, articleAttachmentSb, cancellationToken);
                SetCoverAttachment(postResult, attachmentSb);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Tid:{post.Tid} Pid:{post.Pid}");
                Console.WriteLine(e);

                throw;
            }
        }

        var task = new Task(() => { WriteToFile($"{ARTICLE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_PREFIX, sb); });

        var jsonTask = new Task(() => { WriteToFile($"{ARTICLE_JSON_PATH}/{period.FolderName}", $"{postTableId}.json", "", jsonSb); });

        var attachmentTask = new Task(() => { WriteToFile($"{ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", AttachmentHelper.ATTACHMENT_PREFIX, attachmentSb); });

        var articleAttachmentTask = new Task(() => { WriteToFile($"{ARTICLE_ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", ARTICLE_ATTACHMENT_PREFIX, articleAttachmentSb); });


        task.Start();
        jsonTask.Start();
        attachmentTask.Start();
        articleAttachmentTask.Start();

        await Task.WhenAll(task, jsonTask, attachmentTask, articleAttachmentTask);
    }

    private static async Task SetArticleAsync(ArticlePostResult postResult, StringBuilder sb, StringBuilder jsonSb, StringBuilder attachmentSb, StringBuilder articleAttachmentSb, CancellationToken cancellationToken)
    {
        var post = postResult.Post;

        var isScheduling = post.PostTime < post.Dateline;
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = ReadDic.GetValueOrDefault(post.Tid);
        var imageCount = BbCodeImageRegex.Matches(post.Message).Count;
        var videoCount = BbCodeVideoRegex.Matches(post.Message).Count;

        var article = new Article
                      {
                          Id = postResult.ArticleId,
                          Status = isScheduling ? ArticleStatus.Scheduling : ArticleStatus.Published,
                          DeleteStatus = post.Displayorder switch
                                         {
                                             -1 => DeleteStatus.Deleted,
                                             -2 => DeleteStatus.Deleted,
                                             -3 => DeleteStatus.Deleted,
                                             0 => DeleteStatus.None,
                                             1 => DeleteStatus.None,
                                             2 => DeleteStatus.None,
                                             3 => DeleteStatus.None,
                                             _ => throw new ArgumentOutOfRangeException($"Displayorder", "Not exist")
                                         },
                          Type = post.Special switch
                                 {
                                     1 => ArticleType.Vote,
                                     2 => ArticleType.Diversion,
                                     3 => ArticleType.Reward,
                                     _ => ArticleType.Article
                                 },
                          ContentType = imageCount switch
                                        {
                                            0 when videoCount == 0 => ContentType.PaintText,
                                            > 0 when videoCount > 0 => ContentType.Complex,
                                            _ => imageCount > 0 ? ContentType.Image : ContentType.Video
                                        },
                          PinType = post.Displayorder switch
                                    {
                                        1 => PinType.Board,
                                        2 => PinType.Area,
                                        3 => PinType.Global,
                                        _ => PinType.None
                                    },
                          VisibleType = post.Status == 1 ? VisibleType.Hidden : VisibleType.Public,
                          Title = post.Subject,
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid, postResult.ArticleId, postResult.MemberId, postResult.AttachmentDic, attachmentSb, articleAttachmentSb),
                          ViewCount = post.Views,
                          ReplyCount = post.Replies,
                          BoardId = postResult.BoardId,
                          CategoryId = CategoryDic.GetValueOrDefault(post.Typeid),
                          SortingIndex = postResult.CreateMilliseconds,
                          LastReplyDate = post.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,
                          LastReplierId = post.Lastposter,
                          PinPriority = post.Displayorder,
                          Cover = postResult.ArticleId,
                          Tag = post.Tags.ToNewTags(),
                          RatingCount = post.Ratetimes ?? 0,
                          ShareCount = post.Sharetimes,
                          ImageCount = imageCount,
                          VideoCount = videoCount,
                          DonatePoint = 0,
                          Highlight = post.Highlight != 0,
                          HighlightColor = ColorDic.GetValueOrDefault(highlightInt),
                          Recommend = post.Digest,
                          ReadPermission = post.Readperm,
                          CommentDisabled = post.Closed == 1,
                          CommentVisibleType = post.Status == 34 ? VisibleType.Private : VisibleType.Public,
                          LikeCount = post.ThankCount ?? 0,
                          Ip = post.Useip,
                          Price = post.Price,
                          AuditorId = read?.ReadUid,
                          AuditFloor = read?.ReadFloor,
                          PublishDate = post.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.PostTime.Value) : postResult.CreateDate,
                          HideExpirationDate = BbCodeHideTagRegex.IsMatch(post.Message) ? postResult.CreateDate.AddDays(CommonSetting.HideExpirationDay) : null,
                          PinExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EST")).ToDatetimeOffset(),
                          RecommendExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EDI")).ToDatetimeOffset(),
                          HighlightExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EHL")).ToDatetimeOffset(),
                          CommentDisabledExpirationDate = ModDic.GetValueOrDefault((post.Tid, "ECL")).ToDatetimeOffset(),
                          InVisibleArticleExpirationDate = ModDic.GetValueOrDefault((post.Tid, "BNP")).ToDatetimeOffset() ??
                                                           ModDic.GetValueOrDefault((post.Tid, "UBN")).ToDatetimeOffset(),
                          Signature = post.Usesig,
                          CreatorId = postResult.MemberId,
                          ModifierId = postResult.MemberId,
                          CreationDate = postResult.CreateDate,
                          ModificationDate = postResult.CreateDate
                      };

        sb.AppendValueLine(article.Id, article.BoardId, article.CategoryId.ToCopyValue(), (int) article.Status, (int) article.DeleteStatus,
                           (int) article.VisibleType, (int) article.Type, (int) article.ContentType, (int) article.PinType, article.Title.ToCopyText(),
                           article.Content.ToCopyText(), article.ViewCount, article.ReplyCount, article.SortingIndex, article.LastReplyDate.ToCopyValue(),
                           article.LastReplierId.ToCopyValue(), article.PinPriority,
                           article.Cover.ToCopyValue(), article.Tag, article.RatingCount, article.ShareCount,
                           article.ImageCount, article.VideoCount, article.DonatePoint, article.Highlight, article.HighlightColor.ToCopyValue(),
                           article.Recommend, article.ReadPermission, article.CommentDisabled, (int) article.CommentVisibleType, article.LikeCount,
                           article.Ip, article.Price, article.AuditorId.ToCopyValue(), article.AuditFloor.ToCopyValue(),
                           article.PublishDate, article.HideExpirationDate.ToCopyValue(), article.PinExpirationDate.ToCopyValue(),
                           article.RecommendExpirationDate.ToCopyValue(), article.HighlightExpirationDate.ToCopyValue(), article.CommentDisabledExpirationDate.ToCopyValue(),
                           article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature,
                           article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);

        #region Es文件檔

        jsonSb.Append(ArticleEsIdPrefix).Append(article.Id).AppendLine(ArticleEsIdSuffix);

        var document = SetArticleDocument(article, postResult);

        var json = await JsonHelper.GetJsonAsync(document, cancellationToken);

        jsonSb.AppendLine(json);

        #endregion
    }

    private static void SetCoverAttachment(ArticlePostResult postResult, StringBuilder attachmentSb)
    {
        var post = postResult.Post;

        var isCover = post.Cover is not ("" or "0");
        var externalLink = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb);

        if (string.IsNullOrEmpty(externalLink)) return;

        var attachment = new Attachment
                         {
                             Id = postResult.ArticleId,
                             ExternalLink = externalLink,
                             CreationDate = postResult.CreateDate,
                             CreatorId = postResult.MemberId,
                             ModificationDate = postResult.CreateDate,
                             ModifierId = postResult.MemberId
                         };

        attachmentSb.AppendAttachmentValue(attachment);
    }

    private static Document SetArticleDocument(Article article, ArticlePostResult postResult)
    {
        return new Document()
               {
                   //article part
                   Id = article.Id,
                   Title = RegexHelper.CleanText(article.Title) ?? string.Empty,
                   Content = RegexHelper.CleanText(article.Content) ?? string.Empty,
                   ReadPermission = article.ReadPermission,
                   Tag = article.Tag,
                   ThumbnailId = article.Cover,
                   BoardId = article.BoardId,
                   CategoryId = article.CategoryId,
                   Sequence = 1,
                   SortingIndex = article.SortingIndex,
                   Score = 0,
                   Ip = article.Ip,
                   PinType = article.PinType,
                   PinPriority = 0,
                   VisibleType = article.VisibleType,
                   Status = article.Status,
                   LastReplyDate = article.LastReplyDate,
                   LastReplierId = article.LastReplierId,
                   CreationDate = article.CreationDate,
                   CreatorId = article.CreatorId,
                   ModificationDate = article.ModificationDate,
                   ModifierId = article.ModifierId,

                   //document part
                   Type = DocumentType.Thread,
                   DeleteStatus = article.DeleteStatus,
                   CreatorUid = postResult.Post.Authorid,
                   CreatorName = postResult.MemberName,
                   ModifierUid = postResult.Post.Authorid,
                   ModifierName = postResult.MemberName,
                   Relationship = new Relationship()
                                  {
                                      Name = ArticleRelationShipName
                                  }
               };
    }

    private static void WriteToFile(string directoryPath, string fileName, string copyPrefix, StringBuilder valueSb)
    {
        if (valueSb.Length == 0)
            return;

        var fullPath = $"{directoryPath}/{fileName}";

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(fullPath, string.Concat(copyPrefix, valueSb.ToString()));
        Console.WriteLine(fullPath);
        valueSb.Clear();
    }
}